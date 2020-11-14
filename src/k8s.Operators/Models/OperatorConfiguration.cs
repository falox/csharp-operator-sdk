namespace k8s.Operators
{
    /// <summary>
    /// Represents the operator configuration.
    /// </summary>
    public class OperatorConfiguration
    {
        /// <summary>
        /// Returns the default configuration.
        /// </summary>
        public static OperatorConfiguration Default = new OperatorConfiguration(); // TODO: make readonly

        /// <summary>
        /// The namespace to watch. Set to empty string to watch all namespaces.
        /// </summary>
        public string WatchNamespace { get; set; } = "";

        /// <summary>
        /// The label selector to filter events. Set to null to not filter.
        /// </summary>
        public string WatchLabelSelector { get; set; } = null;

        /// <summary>
        /// The retry policy for the event handling.
        /// </summary>
        public RetryPolicy RetryPolicy { get; set; } = new RetryPolicy();

        /// <summary>
        /// If true, discards the event whose spec generation has already been received and processed
        /// </summary>
        public bool DiscardDuplicateSpecGenerations { get; set; } = true;
    }
}