using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest;
using Microsoft.Extensions.Logging;
using k8s.Operators.Logging;

namespace k8s.Operators
{
    /// <summary>
    /// Represents a Kubernetes operator
    /// </summary>
    public class Operator : Disposable, IOperator
    {
        private const string ALL_NAMESPACES = "";

        private readonly ILogger _logger;
        private readonly IKubernetes _client;
        private readonly Dictionary<(string Namespace, Type ResourceType), IController> _watchedResources;
        private CancellationTokenSource _cts;

        public Operator(IKubernetes client, ILoggerFactory loggerFactory = null)
        {
            this._client = client;
            this._logger = loggerFactory?.CreateLogger<Operator>() ?? SilentLogger.Instance;
            this._watchedResources = new Dictionary<(string Namespace, Type ResourceType), IController>();
            
            TaskScheduler.UnobservedTaskException += (o, ev) =>
            {
                _logger.LogError(ev.Exception, "Unobserved exception");
                ev.SetObserved();
            };
        }

        /// <summary>
        /// Add a controller to handle the events of the custom resource T
        /// </summary>
        /// <param name="controller">The controller for the custom resource</param>
        /// <param name="watchNamespace">The watched namespace. Set to null to watch all namespaces</param>
        /// <typeparam name="T">The type of the custom resource</typeparam>
        public IOperator AddController<T>(IController<T> controller, string watchNamespace = "default") where T : CustomResource
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
            _watchedResources[(watchNamespace, typeof(T))] = controller;

            return this;
        }

        /// <summary>
        /// Starts watching and handling events
        /// </summary>
        public Task StartAsync()
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
                return Task.FromResult(0);
            }

            var watchers = new List<Task>();
            _cts = new CancellationTokenSource();
            
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

            return Task.WhenAll(watchers.ToArray());
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
        public bool IsRunning => _cts?.IsCancellationRequested == false && !IsDisposed;

        /// <summary>
        /// Returns true if Stop or Dispose have been called
        /// </summary>
        /// <returns></returns>
        public bool IsDisposing => _cts?.IsCancellationRequested == true  && !IsDisposed;

        /// <summary>
        /// Watches for events for a given resource definition and namespace. If namespace is empty string, it watches all namespaces
        /// </summary>
        private async Task WatchCustomResourceAsync<T>(CustomResourceDefinitionAttribute crd, string @namespace) where T : CustomResource
        {
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

            using (var watcher = response.Watch<T, object>(OnIncomingEvent, OnWatchError))
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

        protected void OnWatchError(Exception exception)
        {
            if (_cts?.IsCancellationRequested == false)
            {
                _logger.LogError(exception, "Watch Error");
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
            if (_cts?.IsCancellationRequested == false)
            {
                _logger.LogInformation($"Disposing operator");

                // Signal the watchers to stop
                _cts.Cancel();

                // Release resources
                _cts.Dispose();
                _cts = null;
            }
        }
    }
}