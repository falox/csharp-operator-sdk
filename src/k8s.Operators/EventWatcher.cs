using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace k8s.Operators
{
    /// <summary>
    /// Implements the watch callback method for a given <namespace, resource, label selector> 
    /// </summary>
    public class EventWatcher 
    {
        private readonly ILogger _logger;
        private readonly CancellationToken _cancellationToken;

        public Type ResourceType { get; private set; }
        public CustomResourceDefinitionAttribute CRD { get; private set; }
        public string Namespace { get; private set; }
        public string LabelSelector { get; private set; }
        public IController Controller { get; private set; }

        public EventWatcher(Type resourceType, string @namespace, string labelSelector, IController controller, ILogger logger, CancellationToken cancellationToken)
        {
            this.ResourceType = resourceType;
            this.Namespace = @namespace;
            this.LabelSelector = labelSelector;
            this.Controller = controller;
            this._logger = logger;
            this._cancellationToken = cancellationToken;

            // Retrieve the CRD associated to the CR
            var crd = (CustomResourceDefinitionAttribute)Attribute.GetCustomAttribute(resourceType, typeof(CustomResourceDefinitionAttribute));
            this.CRD = crd;
        }

        /// <summary>
        /// Dispatches an incoming event to the controller
        /// </summary>
        public void OnIncomingEvent(WatchEventType eventType, CustomResource resource)
        {
            var resourceEvent = new CustomResourceEvent(eventType, resource);

            _logger.LogDebug($"Received event {resourceEvent}");

            Controller.ProcessEventAsync(resourceEvent, _cancellationToken)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        var exception = t.Exception.Flatten().InnerException;
                        _logger.LogError(exception, $"Error processing {resourceEvent}");
                    }
                });
        }
    }
}