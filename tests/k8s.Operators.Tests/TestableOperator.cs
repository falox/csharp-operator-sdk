using Microsoft.Extensions.Logging;
using System;

namespace k8s.Operators.Tests
{
    public class TestableOperator : Operator
    {
        public TestableOperator(OperatorConfiguration configuration, IKubernetes client, ILoggerFactory loggerFactory = null) : base(configuration, client, loggerFactory)
        {
        }

        /// <summary>
        /// Protected method exposed as Public
        /// </summary>
        public void Exposed_OnIncomingEvent(WatchEventType eventType, CustomResource resource) => OnIncomingEvent(eventType, resource);

        /// <summary>
        /// Protected method exposed as Public
        /// </summary>
        public void Exposed_OnWatchError(Exception exception) => OnWatcherError(exception);

        protected override void OnWatcherClose()
        {
            // HACK: Any watcher will fail and close during tests, since the external Watcher class is not mocked at the moment.
            // This override will ignore the close event and prevent the operator to be stopped prematurely
        }

        public int DisposeInvocationCount { get; private set; }

        protected override void DisposeInternal()
        {
            base.DisposeInternal();

            DisposeInvocationCount++;
        }
    }
}