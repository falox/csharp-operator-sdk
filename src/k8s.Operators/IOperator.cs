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
        /// Adds a controller to handle the events of the custom resource R
        /// </summary>
        /// <param name="controller">The controller for the custom resource</param>
        /// <param name="watchNamespace">The watched namespace. Set to null to watch all namespaces</param>
        /// <typeparam name="R">The type of the custom resource</typeparam>
        IOperator AddController<R>(IController<R> controller, string watchNamespace = "default") where R : CustomResource;

        /// <summary>
        /// Adds a new instance of a controller of type C to handle the events of the custom resource
        /// </summary>
        /// <typeparam name="C">The type of the controller. C must implement IController<R> and expose a constructor that accepts (OperatorConfiguration, IKubernetes, ILoggerFactory)</typeparam>
        /// <returns>The instance of the controller</return>
        IController AddControllerOfType<C>() where C : IController;
        
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