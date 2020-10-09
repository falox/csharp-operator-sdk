using System;
using System.Threading.Tasks;

namespace k8s.Operators
{
    /// <summary>
    /// Represents a Kubernetes operator
    /// </summary>
    public interface IOperator : IDisposable
    {
        /// <summary>
        /// Add a controller to handle the events of the custom resource T
        /// </summary>
        /// <param name="controller">The controller for the custom resource</param>
        /// <param name="watchNamespace">The watched namespace. Set to null to watch all namespaces</param>
        /// <typeparam name="T">The type of the custom resource</typeparam>
        IOperator AddController<T>(IController<T> controller, string watchNamespace = "default") where T : CustomResource;

        /// <summary>
        /// Starts watching and handling events
        /// </summary>
        Task<int> StartAsync();

        /// <summary>
        /// Stops the operator and release the resources. Once stopped, an operator cannot be restarted. Stop() is an alias for Dispose()
        /// </summary>
        void Stop();

        /// <summary>
        /// Returns true if StartAsync has been called and the operator is running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Returns true if Stop/Dispose has been called and not completed
        /// </summary>
        /// <returns></returns>
        bool IsDisposing { get; }
    }
}