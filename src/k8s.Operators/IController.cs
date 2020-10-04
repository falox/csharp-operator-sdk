using System.Threading;
using System.Threading.Tasks;

namespace k8s.Operators
{
    /// <summary>
    /// Controller of a custom resource
    /// </summary>
    public interface IController
    {
        Task ProcessEventAsync(CustomResourceEvent resourceEvent, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Controller of a custom resource of type T
    /// </summary>
    public interface IController<T> : IController where T : CustomResource
    {
    }
}