using System.Threading;
using System.Threading.Tasks;

namespace k8s.Operators
{
    /// <summary>
    /// Controller of a custom resource
    /// </summary>
    public interface IController
    {
        /// <summary>
        /// Processes a custom resource event
        /// </summary>
        /// <param name="resourceEvent">The event to handle</param>
        /// <param name="cancellationToken">Signals if the current execution has been canceled</param>
        Task ProcessEventAsync(CustomResourceEvent resourceEvent, CancellationToken cancellationToken);

        /// <summary>
        /// Retry policy for the controller
        /// </summary>
        RetryPolicy RetryPolicy { get; }
    }

    /// <summary>
    /// Controller of a custom resource of type T
    /// </summary>
    public interface IController<T> : IController where T : CustomResource
    {
    }
}