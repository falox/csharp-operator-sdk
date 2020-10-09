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
        public static OperatorConfiguration Default = new OperatorConfiguration();

        /// <summary>
        /// The namespace to watch. Set to empty string to watch all namespaces.
        /// </summary>
        public string WatchNamespace { get; set; } = "";

        /// <summary>
        /// The retry policy for the event handling.
        /// </summary>
        public RetryPolicy RetryPolicy { get; set; } = new RetryPolicy();
    }
}