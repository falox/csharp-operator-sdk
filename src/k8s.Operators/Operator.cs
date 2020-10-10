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
        private readonly Dictionary<(string Namespace, Type ResourceType), IController> _watchedResources;
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
            this._watchedResources = new Dictionary<(string Namespace, Type ResourceType), IController>();
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
        /// <typeparam name="R">The type of the custom resource</typeparam>
        public IOperator AddController<R>(IController<R> controller, string watchNamespace = "default") where R : CustomResource
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

            // Associate the controller to the namespace
            _watchedResources[(watchNamespace, typeof(R))] = controller;

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
                .Invoke(this, new object[] { controller, _configuration.WatchNamespace });
            
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

            if (_watchedResources.Count == 0)
            {
                _logger.LogDebug($"No controller added, stopping operator");
                Stop();
                return 0;
            }

            _isStarted = true;

            var watchers = new List<Task>();
            
            foreach (var entry in _watchedResources)
            {
                var watchedNamespace = entry.Key.Namespace;
                var watchedResourceType = entry.Key.ResourceType;

                // Retrieve the CRD associated to the CR
                var crd = (CustomResourceDefinitionAttribute)Attribute.GetCustomAttribute(watchedResourceType, typeof(CustomResourceDefinitionAttribute));

                // Invoke WatchCustomResourceAsync() via reflection, since T is in a variable
                var watchCustomResourceAsync = typeof(Operator)
                    .GetMethod("WatchCustomResourceAsync", BindingFlags.NonPublic | BindingFlags.Instance)
                    .MakeGenericMethod(watchedResourceType);

                // Start a watcher for each <resource, namespace>
                var watcher = ((Task) watchCustomResourceAsync.Invoke(this, new object[] { crd, watchedNamespace }))
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            _logger.LogError(t.Exception.Flatten().InnerException, $"Error watching {watchedNamespace}/{crd.Plural}");
                        }
                    });
                
                watchers.Add(watcher);
            }

            await Task.WhenAll(watchers.ToArray());

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
        private async Task WatchCustomResourceAsync<T>(CustomResourceDefinitionAttribute crd, string @namespace) where T : CustomResource
        {
            if (IsDisposing || IsDisposed)
            {
                return;
            }

            var response = await _client.ListNamespacedCustomObjectWithHttpMessagesAsync(
                crd.Group,
                crd.Version,
                @namespace,
                crd.Plural,
                watch: true,
                timeoutSeconds: (int)TimeSpan.FromMinutes(60).TotalSeconds,
                cancellationToken: _cts.Token
            ).ConfigureAwait(false);

            _logger.LogDebug($"Begin watch {@namespace}/{crd.Plural}");

            using (var watcher = response.Watch<T, object>(OnIncomingEvent, OnWatcherError, OnWatcherClose))
            {
                await WaitOneAsync(_cts.Token.WaitHandle);

                _logger.LogDebug($"End watch {@namespace}/{crd.Plural}");
            }
        }

        /// <summary>
        /// Dispatches an incoming event to the right controller
        /// </summary>
        /// <param name="eventType"></param>
        /// <param name="resource"></param>
        protected void OnIncomingEvent(WatchEventType eventType, CustomResource resource)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException("Operator");
            }

            var resourceEvent = new CustomResourceEvent(eventType, resource);

            _logger.LogDebug($"Received event {resourceEvent}");

            IController controller = null;

            // Retrieve the controller for the exact namespace
            if (!_watchedResources.TryGetValue((resource.Metadata.NamespaceProperty, resource.GetType()), out controller))
            {
                // If not found, retrieve the generic "all namespaces" controller
                _watchedResources.TryGetValue((ALL_NAMESPACES, resource.GetType()), out controller);
            }

            if (controller == null)
            {
                _logger.LogDebug($"Discarded event {resourceEvent}, no matching controller");
                return;
            }

            controller.ProcessEventAsync(resourceEvent, _cts.Token)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        var exception = t.Exception.Flatten().InnerException;
                        _logger.LogError(exception, $"Error processing {resourceEvent}");
                    }
                });
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