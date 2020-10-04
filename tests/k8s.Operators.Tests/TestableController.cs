using k8s.Operators;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace k8s.Operators.Tests
{

    public class TestableController : Controller<TestableCustomResource>
    {
        public TestableController() : base(null, null)
        {
        }

        public TestableController(IKubernetes client, ILoggerFactory loggerFactory = null) : base(client, loggerFactory)
        {
        }

        public List<(TestableCustomResource resource, bool deleted)> Invocations = new List<(TestableCustomResource resource, bool deleted)>();

        public List<TestableCustomResource> Invocations_AddOrModify = new List<TestableCustomResource>();
        
        public List<TestableCustomResource> Invocations_Delete = new List<TestableCustomResource>();
        
        private TaskCompletionSource<object> _tcs = null;

        protected override async Task AddOrModifyAsync(TestableCustomResource resource, CancellationToken cancellationToken)
        {
            Invocations_AddOrModify.Add(resource);
            Invocations.Add((resource, false));

            if (_tcs != null)
            {
                // Wait for Unblock()
                await _tcs?.Task;
            }
        }

        protected override async Task DeleteAsync(TestableCustomResource resource, CancellationToken cancellationToken)
        {
            Invocations_Delete.Add(resource);
            Invocations.Add((resource, true));

            if (_tcs != null)
            {
                // Wait for Unblock()
                await _tcs?.Task;
            }
        }

        /// <summary>
        /// Protected method exposed as Public
        /// </summary>
        public Task<TestableCustomResource> Exposed_UpdateResourceAsync(TestableCustomResource resource, CancellationToken cancellationToken) => UpdateResourceAsync(resource, cancellationToken);

        /// <summary>
        /// Protected method exposed as Public
        /// </summary>
        public Task<TestableCustomResource> Exposed_UpdateStatusAsync(TestableCustomResource resource, CancellationToken cancellationToken) => UpdateStatusAsync(resource, cancellationToken);

        /// <summary>
        /// Block the next call to AddOrModifyAsync or DeleteAsync
        /// </summary>
        public TaskCompletionSource<object> BlockNextCall()
        {
            _tcs = new TaskCompletionSource<object>();
            return _tcs;
        }

        /// <summary>
        /// Unblock the next call to AddOrModifyAsync or DeleteAsync
        /// </summary>
        public void Unblock(TaskCompletionSource<object> tcs)
        {
            tcs.SetResult(true);
        }
    }
}
