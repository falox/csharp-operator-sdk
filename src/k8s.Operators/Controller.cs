using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using k8s.Operators.Logging;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace k8s.Operators
{
    /// <summary>
    /// Controller of a custom resource of type T
    /// </summary>
    public abstract class Controller<T> : IController<T> where T : CustomResource
    {
        protected readonly ILogger _logger;
        protected readonly IKubernetes _client;
        private readonly EventManager _eventManager;
        private readonly ResourceChangeTracker _changeTracker;
        private readonly CustomResourceDefinitionAttribute _crd;

        public Controller(OperatorConfiguration configuration, IKubernetes client, ILoggerFactory loggerFactory = null)
        {
            this._client = client;
            this._logger = loggerFactory?.CreateLogger<Controller<T>>() ?? SilentLogger.Instance;
            this._eventManager = new EventManager(loggerFactory);
            this._changeTracker = new ResourceChangeTracker(loggerFactory);
            this._crd = (CustomResourceDefinitionAttribute) Attribute.GetCustomAttribute(typeof(T), typeof(CustomResourceDefinitionAttribute));
            this.RetryPolicy = configuration.RetryPolicy;
        }

        /// <summary>
        /// Retry policy for the controller
        /// </summary>
        public RetryPolicy RetryPolicy { get; protected set; }

        /// <summary>
        /// Processes a custom resource event
        /// </summary>
        /// <param name="resourceEvent">The event to handle</param>
        /// <param name="cancellationToken">Signals if the current execution has been canceled</param>
        public async Task ProcessEventAsync(CustomResourceEvent resourceEvent, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Begin ProcessEvent, {resourceEvent}");

            if (resourceEvent.Type == WatchEventType.Error)
            {
                _logger.LogError($"Received Error event, {resourceEvent.Resource}");
                return;
            }

            if (resourceEvent.Type == WatchEventType.Deleted)
            {
                // Skip Deleted events since there is nothing else to do
                _logger.LogDebug($"Skip ProcessEvent, received Deleted event, {resourceEvent.Resource}");
                return;
            }

            if (resourceEvent.Type == WatchEventType.Bookmark)
            {
                // Skip Bookmark events since there is nothing else to do
                _logger.LogDebug($"Skip ProcessEvent, received Bookmark event, {resourceEvent.Resource}");
                return;
            }

            // Enqueue the event
            _eventManager.Enqueue(resourceEvent);

            while (!cancellationToken.IsCancellationRequested)
            {
                // Dequeue the next event to process for this resource, if any
                var nextEvent =_eventManager.Dequeue(resourceEvent.ResourceUid);
                if (nextEvent == null)
                {
                    break;
                }
                
                await HandleEventAsync(nextEvent, cancellationToken);
            }

            _logger.LogDebug($"End ProcessEvent, {resourceEvent}");
        }
        
        private async Task HandleEventAsync(CustomResourceEvent resourceEvent, CancellationToken cancellationToken)
        {
            if (resourceEvent == null)
            {
                _logger.LogWarning($"Skip HandleEvent, {nameof(resourceEvent)} is null");
                return;
            }

            _logger.LogDebug($"Begin HandleEvent, {resourceEvent}");

            _eventManager.BeginHandleEvent(resourceEvent);

            var attempt = 1;
            var delay = RetryPolicy.InitialDelay;
            while (true)
            {
                // Try to handle the event
                var handled = await TryHandleEventAsync(resourceEvent, cancellationToken);
                if (handled)
                {
                    break;
                }

                // Something went wrong
                if (!CanTryAgain(resourceEvent, attempt, cancellationToken))
                {                    
                    break;
                }

                _logger.LogDebug($"Retrying to handle {resourceEvent} in {delay}ms (attempt #{attempt})");

                // Wait
                await Task.Delay(delay);

                // Increase the delay for the next attempt
                attempt++;
                delay = (int)(delay * RetryPolicy.DelayMultiplier);                    
            }

            _logger.LogDebug($"End HandleEvent, {resourceEvent}");

            _eventManager.EndHandleEvent(resourceEvent);
        }

        private bool CanTryAgain(CustomResourceEvent resourceEvent, int attemptNumber, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug($"Cannot retry {resourceEvent}, processing has been canceled");
                return false;
            }

            var upcoming = _eventManager.Peek(resourceEvent.ResourceUid);
            if (upcoming != null)
            {
                _logger.LogDebug($"Cannot retry {resourceEvent}, received {upcoming} in the meantime");
                return false;
            }

            if (attemptNumber > RetryPolicy.MaxAttempts)
            {
                _logger.LogDebug($"Cannot retry {resourceEvent}, max number of attempts reached");
                return false;
            }

            return true;
        }

        private async Task<bool> TryHandleEventAsync(CustomResourceEvent resourceEvent, CancellationToken cancellationToken)
        {
            bool handled = true;

            try
            {
                var resource = (T)resourceEvent.Resource;

                if (IsDeletePending(resource))
                {
                    await HandleDeletedEventAsync(resource, cancellationToken);
                }
                else
                {
                    await HandleAddedOrModifiedEventAsync(resource, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug($"Canceled HandleEvent, {resourceEvent}");
            }
            catch (Exception exception)
            {
                if (exception is HttpOperationException httpException && httpException.Response?.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    // Conflicts happen. The next event will make the resource consistent again
                    _logger.LogDebug(exception, $"Conflict handling {resourceEvent}");
                }
                else
                {
                    _logger.LogError(exception, $"Error handling {resourceEvent}");
                    handled = false;
                }
            }

            return handled;
        }

        private async Task HandleAddedOrModifiedEventAsync(T resource, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Handle Added/Modified, {resource}");

            if (!HasFinalizer(resource))
            {
                // Before any custom logic, add a finalizer to be used later during the deletion phase
                _logger.LogDebug($"Add missing finalizer");
                await AddFinalizerAsync(resource, cancellationToken);
                return;
            }

            if (_changeTracker.IsResourceGenerationAlreadyHandled(resource))
            {
                _logger.LogDebug($"Skip AddOrModifyAsync, {resource} already handled");
            }
            else
            {
                _logger.LogDebug($"Begin AddOrModifyAsync, {resource}");

                // Add/modify logic (implemented by the derived class)
                await AddOrModifyAsync(resource, cancellationToken);

                _changeTracker.TrackResourceGenerationAsHandled(resource);

                _logger.LogDebug($"End AddOrModifyAsync, {resource}");
            }
        }

        private async Task HandleDeletedEventAsync(T resource, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Handle Deleted, {resource}");

            if (!HasFinalizer(resource))
            {
                // The current deletion request is not handled by this controller
                _logger.LogDebug($"Skip OnDeleted, {resource} has no finalizer");
                return;
            }

            _logger.LogDebug($"Begin OnDeleted, {resource}");

            // Delete logic (implemented by the derived class)
            await DeleteAsync(resource, cancellationToken);

            _changeTracker.TrackResourceGenerationAsDeleted(resource);

            if (HasFinalizer(resource))
            {
                await RemoveFinalizerAsync(resource, cancellationToken);
            }

            _logger.LogDebug($"End OnDeleted, {resource}");
        }

        /// <summary>
        /// Implements the logic to add or modify a resource
        /// </summary>
        /// <param name="resource">Resource being added or modified</param>
        /// <param name="cancellationToken">Signals if the current execution has been canceled</param>
        [ExcludeFromCodeCoverage]
        protected virtual Task AddOrModifyAsync(T resource, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        /// <summary>
        /// Implements the logic to delete a resource
        /// </summary>
        /// <param name="resource">Resource being deleted</param>
        /// <param name="cancellationToken">Signals if the current execution has been canceled</param>
        /// <returns></returns>
        [ExcludeFromCodeCoverage]
        protected virtual Task DeleteAsync(T resource, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        /// <summary>
        /// Updates the status subresource
        /// </summary>
        /// <see cref="https://kubernetes.io/docs/tasks/extend-kubernetes/custom-resources/custom-resource-definitions/#status-subresource"/>
        protected Task<T> UpdateStatusAsync<R>(R resource, CancellationToken cancellationToken) where R : T, IStatus
        {
            // Build the delta JSON
            var patch = new JsonPatchDocument<R>().Replace(x => x.Status, resource.Status);

            return PatchCustomResourceStatusAsync(resource, patch, cancellationToken);
        }

        /// <summary>
        /// Updates the resource (except the status)
        /// </summary>
        protected Task<T> UpdateResourceAsync(T resource, CancellationToken cancellationToken)
        {
            return ReplaceCustomResourceAsync(resource, cancellationToken);
        }

        private bool IsDeletePending(CustomResource resource)
        {
            return resource.Metadata.DeletionTimestamp != null;
        }

        private bool HasFinalizer(CustomResource resource)
        {
            return resource.Metadata.Finalizers?.Contains(_crd.Finalizer) == true;
        }

        private Task<T> AddFinalizerAsync(T resource, CancellationToken cancellationToken)
        {
            // Add the finalizer
            resource.Metadata.EnsureFinalizers().Add(_crd.Finalizer);

            return ReplaceCustomResourceAsync(resource, cancellationToken);
        }

        private Task<T> RemoveFinalizerAsync(T resource, CancellationToken cancellationToken)
        {
            // Remove the finalizer
            resource.Metadata.Finalizers.Remove(_crd.Finalizer);

            return ReplaceCustomResourceAsync(resource, cancellationToken);
        }

        private async Task<T> ReplaceCustomResourceAsync(T resource, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Replace Custom Resource, {(resource == null ? "" : JsonConvert.SerializeObject(resource))}");

            // Replace the resource
            var result = await _client.ReplaceNamespacedCustomObjectAsync(
                resource,
                _crd.Group, 
                _crd.Version, 
                resource.Metadata.NamespaceProperty, 
                _crd.Plural, 
                resource.Metadata.Name,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            return ToCustomResource(result);
        }

        private async Task<T> PatchCustomResourceStatusAsync<R>(R resource, IJsonPatchDocument patch, CancellationToken cancellationToken) where R : T, IStatus
        {
            _logger.LogDebug($"Patch Status, {(patch == null ? "" : JsonConvert.SerializeObject(patch))}");

            // Patch the status
            var result = await _client.PatchNamespacedCustomObjectStatusAsync(
                new V1Patch(patch), 
                _crd.Group, 
                _crd.Version, 
                resource.Metadata.NamespaceProperty, 
                _crd.Plural, 
                resource.Metadata.Name,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            return ToCustomResource(result);
        }

        private T ToCustomResource(object input)
        {
            T result = default(T);

            if (input is JObject json)
            {
                result = json.ToObject<T>();
            }
            else
            {
                result = (T)input;
            }

            return result;
        }
    }
}