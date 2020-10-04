namespace k8s.Operators
{
    /// <summary>
    /// Represents a custom resource event
    /// </summary>
    public class CustomResourceEvent
    {
        public CustomResourceEvent(WatchEventType type, CustomResource resource)
        {
            Type = type;
            Resource = resource;
        }

        /// <summary>
        /// The type of the event
        /// </summary>
        /// <value></value>
        public WatchEventType Type { get; }
        
        /// <summary>
        /// The watched custom resource
        /// </summary>
        /// <value></value>
        public CustomResource Resource { get; }
        
        /// <summary>
        /// Returns the Uid of the custom resource
        /// </summary>
        public string ResourceUid => Resource.Metadata.Uid;

        public override string ToString()
        {
            return $"{Type} {Resource}";
        }
    }
}