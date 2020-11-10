using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest;
using Microsoft.Extensions.Logging;
using k8s.Operators.Logging;
using System.Diagnostics.CodeAnalysis;

namespace k8s.Operators
{
    /// <summary>
    /// Represents a Kubernetes operator
    /// </summary>
    public class Operator : Disposable, IOperator
    {
        private const string ALL_NAMESPACES = "";

        private readonly ILogger _logger;
        private readonly OperatorConfiguration _configuration;
        private readonly IKubernetes _client;
        protected readonly List<EventWatcher> _watchers;
        private readonly CancellationTokenSource _cts;
        private readonly ILoggerFactory _loggerFactory;
        private bool _isStarted;
        private bool _unexpectedWatcherTermination;

        public Operator(OperatorConfiguration configuration, IKubernetes client, ILoggerFactory loggerFactory = null)
        {
            this._configuration = configuration;
            this._client = client;
            this._loggerFactory = loggerFactory;
            this._logger = loggerFactory?.CreateLogger<Operator>() ?? SilentLogger.Instance;
            this._watchers = new List<EventWatcher>();
            this._cts = new CancellationTokenSource();
            
            TaskScheduler.UnobservedTaskException += (o, ev) =>
            {
                _logger.LogError(ev.Exception, "Unobserved exception");
                ev.SetObserved();
            };

            // TODO: log versions
        }

        /// <summary>
        /// Adds a controller to handle the events of the custom resource R
        /// </summary>
        /// <param name="controller">The controller for the custom resource</param>
        /// <param name="watchNamespace">The watched namespace. Set to null to watch all namespaces</param>
        /// <param name="labelSelector">The <see href="https://kubernetes.io/docs/concepts/overview/working-with-objects/labels/#list-and-watch-filtering">label selector</see> to filter the sets of events returned/></param>
        /// <typeparam name="R">The type of the custom resource</typeparam>
        public IOperator AddController<R>(IController<R> controller, string watchNamespace = "default", string labelSelector = null) where R : CustomResource
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException("Operator");
            }

            if (controller == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, nameof(controller));
            }

            if (IsRunning)
            {
                throw new InvalidOperationException("A controller cannot be added once the operator has started");
            }

            if (watchNamespace == null)
            {
                watchNamespace = ALL_NAMESPACES;
            }

            _logger.LogDebug($"Added controller {controller} on namespace {(string.IsNullOrEmpty(watchNamespace) ? "\"\"" : watchNamespace)}");

            _watchers.Add(new EventWatcher(typeof(R), watchNamespace, labelSelector, controller, _logger, _cts.Token));

            return this;
        }

        /// <summary>
        /// Adds a new instance of a controller of type C to handle the events of the custom resource
        /// </summary>
        /// <typeparam name="C">The type of the controller. C must implement IController<R> and expose a constructor that accepts (OperatorConfiguration, IKubernetes, ILoggerFactory)</typeparam>
        /// <returns>The instance of the controller</return>
        public IController AddControllerOfType<C>() where C : IController
        {
            // Use Reflection to instantiate the controller and pass it to AddController<R>()

            // ASSUMPTION: C implements IController<R>, where R is a custom resource

            // Retrieve the type of R
            var R = typeof(C).BaseType.GetGenericArguments()[0];
            
            // Instantiate the controller implementing IController<R> via the standard constructor (OperatorConfiguration, IKubernetes, ILoggerFactory)
            object controller = Activator.CreateInstance(typeof(C), _configuration, _client, _loggerFactory);

            // Invoke AddController<R>()
            typeof(Operator)
                .GetMethod("AddController")
                .MakeGenericMethod(R)
                .Invoke(this, new object[] { controller, _configuration.WatchNamespace, _configuration.WatchLabelSelector });
            
            return (IController) controller;
        }

        /// <summary>
        /// Starts watching and handling events
        /// </summary>
        public async Task<int> StartAsync()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException("Operator");
            }

            _logger.LogInformation($"Start operator");

            if (_watchers.Count == 0)
            {
                _logger.LogDebug($"No controller added, stopping operator");
                Stop();
                return 0;
            }

            _isStarted = true;

            var tasks = new List<Task>();
            
            foreach (var entry in _watchers)
            {
                // Invoke WatchCustomResourceAsync() via reflection, since T is in a variable
                var watchCustomResourceAsync = typeof(Operator)
                    .GetMethod("WatchCustomResourceAsync", BindingFlags.NonPublic | BindingFlags.Instance)
                    .MakeGenericMethod(entry.ResourceType);

                // Start a watcher for each <resource, namespace, labelSelector>
                var watcher = ((Task) watchCustomResourceAsync.Invoke(this, new object[] { entry }))
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            _logger.LogError(t.Exception.Flatten().InnerException, $"Error watching {entry.Namespace}/{entry.CRD.Plural} {entry.LabelSelector}");
                        }
                    });
                
                tasks.Add(watcher);
            }

            await Task.WhenAll(tasks.ToArray());

            return _unexpectedWatcherTermination ? 1 : 0;
        }

        /// <summary>
        /// Stops the operator and release the resources. Once stopped, an operator cannot be restarted. Stop() is an alias for Dispose()
        /// </summary>
        public void Stop()
        {
            _logger.LogInformation($"Stop operator");
            Dispose();
        }

        /// <summary>
        /// Returns true if StartAsync has been called and the operator is running
        /// </summary>
        public bool IsRunning => !IsDisposing && !IsDisposed && _isStarted;

        /// <summary>
        /// Watches for events for a given resource definition and namespace. If namespace is empty string, it watches all namespaces
        /// </summary>
        private async Task WatchCustomResourceAsync<T>(EventWatcher watcher) where T : CustomResource
        {
            if (IsDisposing || IsDisposed)
            {
                return;
            }

            var response = await _client.ListNamespacedCustomObjectWithHttpMessagesAsync(
                watcher.CRD.Group,
                watcher.CRD.Version,
                watcher.Namespace,
                watcher.CRD.Plural,
                watch: true,
                labelSelector: watcher.LabelSelector,
                timeoutSeconds: (int)TimeSpan.FromMinutes(60).TotalSeconds,
                cancellationToken: _cts.Token
            ).ConfigureAwait(false);

            _logger.LogDebug($"Begin watch {watcher.Namespace}/{watcher.CRD.Plural} {watcher.LabelSelector}");

            using (var _ = response.Watch<T, object>(watcher.OnIncomingEvent, OnWatcherError, OnWatcherClose))
            {
                await WaitOneAsync(_cts.Token.WaitHandle);

                _logger.LogDebug($"End watch {watcher.Namespace}/{watcher.CRD.Plural} {watcher.LabelSelector}");
            }
        }
        
        [ExcludeFromCodeCoverage]
        protected void OnWatcherError(Exception exception)
        {
            if (IsRunning)
            {
                _logger.LogError(exception, "Watcher error");
            }
        }

        [ExcludeFromCodeCoverage]
        protected virtual void OnWatcherClose()
        {
            _logger.LogError("Watcher closed");

            if (IsRunning)
            {
                // At least one watcher stopped unexpectedly. Stop the operator, let Kubernetes restart it
                _unexpectedWatcherTermination = true;
                Stop();
            }
        }

        /// <summary>
        /// Returns a Task wrapper for a synchronous wait on a wait handle
        /// </summary>
        /// <see cref="https://msdn.microsoft.com/en-us/library/hh873178%28v=vs.110%29.aspx#WHToTap"/>
        private Task<bool> WaitOneAsync(WaitHandle waitHandle, int millisecondsTimeOutInterval = Timeout.Infinite)
        {
            if (waitHandle == null)
            {
                throw new ArgumentNullException(nameof(waitHandle));
            }

            var tcs = new TaskCompletionSource<bool>();

            var rwh = ThreadPool.RegisterWaitForSingleObject(
                waitHandle,
                callBack: (state, timedOut) => { tcs.TrySetResult(!timedOut); }, 
                state: null,
                millisecondsTimeOutInterval: millisecondsTimeOutInterval, 
                executeOnlyOnce: true
            );

            var task = tcs.Task;

            task.ContinueWith(t =>
            {
                rwh.Unregister(waitObject: null);
                try
                {
                    return t.Result;
                }
                catch 
                {
                    return false;
                    throw;
                }
            });

            return task;
        }

        protected override void DisposeInternal()
        {
            _logger.LogInformation($"Disposing operator");

            // Signal the watchers to stop
            _cts.Cancel();
        }
    }
}