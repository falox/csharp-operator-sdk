using Microsoft.Extensions.Logging;
using System;

namespace k8s.Operators.Tests
{
    public class TestableOperator : Operator
    {
        public TestableOperator(IKubernetes client, ILoggerFactory loggerFactory = null) : base(client, loggerFactory)
        {
        }

        /// <summary>
        /// Protected method exposed as Public
        /// </summary>
        public void Exposed_OnIncomingEvent(WatchEventType eventType, CustomResource resource) => OnIncomingEvent(eventType, resource);

        /// <summary>
        /// Protected method exposed as Public
        /// </summary>
        public void Exposed_OnWatchError(Exception exception) => OnWatchError(exception);
    }
}
