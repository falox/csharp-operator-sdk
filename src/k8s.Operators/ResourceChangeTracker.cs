using System.Collections.Generic;
using k8s.Operators.Logging;
using Microsoft.Extensions.Logging;

namespace k8s.Operators
{
    /// <summary>
    /// Keeps track of the resource changes to avoid unnecessary updates
    /// </summary>
    public class ResourceChangeTracker
    {
        private readonly ILogger _logger;

        // Last generation number successfully processed, for each resource
        private readonly Dictionary<string, long> _lastResourceGenerationProcessed;

        public ResourceChangeTracker(ILoggerFactory loggerFactory)
        {
            this._logger = loggerFactory?.CreateLogger<ResourceChangeTracker>() ?? SilentLogger.Instance;
            this._lastResourceGenerationProcessed = new Dictionary<string, long>();
        }

        /// <summary>
        /// Returns true if the same resource/generation has already been handled
        /// </summary>
        public bool IsResourceGenerationAlreadyHandled(CustomResource resource)
        {
            bool processedInPast = _lastResourceGenerationProcessed.TryGetValue(resource.Metadata.Uid, out long resourceGeneration);

            return processedInPast 
                && resource.Metadata.Generation != null 
                && resourceGeneration >= resource.Metadata.Generation.Value;
        }

        /// <summary>
        /// Mark a resource generation as successfully handled
        /// </summary>
        public void TrackResourceGenerationAsHandled(CustomResource resource)
        {
            if (resource.Metadata.Generation != null)
            {
                _lastResourceGenerationProcessed[resource.Metadata.Uid] = resource.Metadata.Generation.Value;
            }
        }

        /// <summary>
        /// Mark a resource generation as successfully deleted
        /// </summary>
        public void TrackResourceGenerationAsDeleted(CustomResource resource)
        {
            _lastResourceGenerationProcessed.Remove(resource.Metadata.Uid);
        }   
    }
}