using System;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace k8s.Operators.Samples.Basic
{
    public class MyResourceController : Controller<MyResource>
    {        
        public MyResourceController(IKubernetes client, ILoggerFactory loggerFactory = null) : base(client, loggerFactory)
        {
        }

        protected override async Task AddOrModifyAsync(MyResource resource, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Begin AddOrModify {resource}");
            
            try
            {
                // Simulate event handling
                await Task.Delay(5000, cancellationToken);

                // Update the resource
                resource.Metadata.EnsureAnnotations()["custom-key"] = DateTime.UtcNow.ToString("s");
                await UpdateResourceAsync(resource, cancellationToken);

                // Update the status
                if (resource.Status == null)
                {
                    resource.Status = new MyResource.MyResourceStatus();
                }
                resource.Status.Actual = resource.Spec.Desired;
                await UpdateStatusAsync(resource, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"Interrupted! Trying to shutdown gracefully...");

                // Simulate a blocking operation
                Task.Delay(3000).Wait();
            }

            _logger.LogInformation($"End AddOrModify {resource}");
        }

        protected override async Task DeleteAsync(MyResource resource, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Begin Delete {resource}");
            
            try
            {
                // Simulate event handling
                await Task.Delay(5000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"Interrupted! Trying to shutdown gracefully...");

                // Simulate a blocking operation
                Task.Delay(3000).Wait();
            }

            _logger.LogInformation($"End Delete {resource}");
        }
    }
}