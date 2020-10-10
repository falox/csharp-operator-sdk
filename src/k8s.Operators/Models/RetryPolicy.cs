namespace k8s.Operators
{
    /// <summary>
    /// Represents a retry policy for a custom resource controller
    /// </summary>
    public class RetryPolicy
    {
        /// <summary>
        /// Max number of attempts
        /// </summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// Initial time delay (in milliseconds) before to process again the event.
        /// After an attempt, the delay is incresead by multiplying it by DelayMultiplier
        /// </summary>
        public int InitialDelay { get; set; } = 5000;

        /// <summary>
        /// The multiplier applied to the delay after each attempt. 
        /// DelayMultiplier = 1 keeps the delay constant
        /// </summary>
        public double DelayMultiplier { get; set; } = 1.5;
    }
}