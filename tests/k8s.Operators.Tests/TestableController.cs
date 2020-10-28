using k8s.Operators;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System;

namespace k8s.Operators.Tests
{
    public class TestableController : Controller<TestableCustomResource>
    {
        public TestableController() : base(OperatorConfiguration.Default, null, null)
        {
        }

        public TestableController(IKubernetes client, ILoggerFactory loggerFactory = null) : base(OperatorConfiguration.Default, client, loggerFactory)
        {
        }

        public TestableController(OperatorConfiguration configuration, IKubernetes client, ILoggerFactory loggerFactory = null) : base(configuration, client, loggerFactory)
        {
        }

        public List<TestableCustomResource> Invocations_AddOrModify = new List<TestableCustomResource>();
        public List<TestableCustomResource> Invocations_Delete = new List<TestableCustomResource>();
        public List<(TestableCustomResource resource, bool deleteEvent)> Invocations = new List<(TestableCustomResource resource, bool deleteEvent)>();        
        public List<(TestableCustomResource resource, bool deleteEvent)> CompletedEvents = new List<(TestableCustomResource resource, bool deleteEvent)>();

        private Queue<TaskCompletionSource<object>> _signals = new Queue<TaskCompletionSource<object>>();

        protected override async Task AddOrModifyAsync(TestableCustomResource resource, CancellationToken cancellationToken)
        {
            Invocations_AddOrModify.Add(resource);
            Invocations.Add((resource, false));

            if (_signals.TryDequeue(out var signal))
            {
                // Wait for UnblockEvent()
                await signal?.Task;
            }

            if (_exceptionsToThrow > 0)
            {
                _exceptionsToThrow--;
                throw new Exception();
            }

            CompletedEvents.Add((resource, deleteEvent: false));
        }

        protected override async Task DeleteAsync(TestableCustomResource resource, CancellationToken cancellationToken)
        {
            Invocations_Delete.Add(resource);
            Invocations.Add((resource, true));

            if (_signals.TryDequeue(out var signal))
            {
                // Wait for UnblockEvent()
                await signal?.Task;
            }

            if (_exceptionsToThrow > 0)
            {
                _exceptionsToThrow--;
                throw new Exception();
            }

            CompletedEvents.Add((resource, deleteEvent: true));
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
        /// Throws an exception in the next calls to AddOrModifyAsync or DeleteAsync
        /// </summary>
        /// <param name="count">The number of the events to make fail</param>
        public void ThrowExceptionOnNextEvents(int count)
        {
            _exceptionsToThrow = count;
        }

        private int _exceptionsToThrow = 0;

        /// <summary>
        /// Block the next call to AddOrModifyAsync or DeleteAsync
        /// </summary>
        public TaskCompletionSource<object> BlockNextEvent()
        {
            var signal = new TaskCompletionSource<object>();
            _signals.Enqueue(signal);
            return signal;
        }

        /// <summary>
        /// Unblock the next call to AddOrModifyAsync or DeleteAsync
        /// </summary>
        public void UnblockEvent(TaskCompletionSource<object> signal)
        {
            signal.SetResult(true);
        }
    }
}
