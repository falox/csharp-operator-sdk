using System.Collections.Generic;
using k8s.Operators.Logging;
using Microsoft.Extensions.Logging;

namespace k8s.Operators
{
    /// <summary>
    /// Manages the event queues for the watched resources
    /// </summary>
    public class EventManager
    {
        private ILogger _logger;

        // Next event to handle, for each resource. 
        // A real queue is not used since intermediate events are discarded and only the queue head is stored.
        private Dictionary<string, CustomResourceEvent> _queuesByResource;

        // Events that are currently being handled
        private Dictionary<string, CustomResourceEvent> _handling;

        public EventManager(ILoggerFactory loggerFactory)
        {
            this._logger = loggerFactory?.CreateLogger<EventManager>() ?? SilentLogger.Instance;
            this._queuesByResource = new Dictionary<string, CustomResourceEvent>();
            this._handling = new Dictionary<string, CustomResourceEvent>();
        }

        /// <summary>
        /// Enqueue the event
        /// </summary>
        public void Enqueue(CustomResourceEvent resourceEvent)
        {
            lock (this)
            {
                _logger.LogTrace($"Enqueue {resourceEvent}");
                // Insert or update the next event for the resource
                _queuesByResource[resourceEvent.ResourceUid] = resourceEvent;
            }
        }

        /// <summary>
        /// Pops and returns the next event to process, if any
        /// </summary>
        public CustomResourceEvent Dequeue(string resourceUid)
        {
            lock (this)
            {
                if (IsHandling(resourceUid, out var handlingEvent))
                {
                    _logger.LogDebug($"Postponed Dequeue, handling {handlingEvent}");
                    return null;
                }
                else
                {
                    if (_queuesByResource.TryGetValue(resourceUid, out CustomResourceEvent result))
                    {
                        _queuesByResource.Remove(resourceUid);
                        _logger.LogTrace($"Dequeue {result}");
                    }
                    return result;
                }
            }
        }

        /// <summary>
        /// Track the begin of an event handling
        /// </summary>
        public void BeginHandleEvent(CustomResourceEvent resourceEvent)
        {
            lock (this)
            {
                _logger.LogTrace($"BeginHandleEvent {resourceEvent}");
                _handling[resourceEvent.ResourceUid] = resourceEvent;
            }
        }

        /// <summary>
        /// Track the end of an event handling
        /// </summary>
        public void EndHandleEvent(CustomResourceEvent resourceEvent)
        {
            lock (this)
            {
                _logger.LogTrace($"EndHandleEvent {resourceEvent}");
                _handling.Remove(resourceEvent.ResourceUid);
            }
        }

        /// <summary>
        /// Returns true if there is an event being handled
        /// </summary>
        private bool IsHandling(string resourceUid, out CustomResourceEvent handlingEvent)
        {
            return _handling.TryGetValue(resourceUid, out handlingEvent);
        }
    }
}