using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.Rest;
using Moq;
using Xunit;

namespace k8s.Operators.Tests
{
    public class BaseTests
    {
        protected CancellationToken DUMMY_TOKEN = default(CancellationToken);

        protected Mock<IKubernetes> _clientMock;
        protected IKubernetes _client => _clientMock.Object;

        public BaseTests()
        {
            // Setup the client mock
            _clientMock = new Mock<IKubernetes>();

            _clientMock.Setup(x => x.ListNamespacedCustomObjectWithHttpMessagesAsync(
                    It.IsAny<string>(), 
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    It.IsAny<int?>(), 
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<System.Threading.CancellationToken>()
                )
            ).Returns(Task.FromResult(new HttpOperationResponse<object>()));

            _clientMock.Setup(x => x.ReplaceNamespacedCustomObjectWithHttpMessagesAsync(
                    It.IsAny<object>(), 
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<System.Threading.CancellationToken>()
                )
            ).Returns(Task.FromResult(new HttpOperationResponse<object>()));

            _clientMock.Setup(x => x.PatchNamespacedCustomObjectStatusWithHttpMessagesAsync(
                    It.IsAny<object>(), 
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<System.Threading.CancellationToken>()
                )
            ).Returns(Task.FromResult(new HttpOperationResponse<object>()));
        }

        protected TestableCustomResource CreateCustomResource(string uid = "1", string ns = null, long generation = 1L, bool withFinalizer = true, DateTime? deletionTimeStamp = null)
        {
            var resource = new TestableCustomResource();

            if (withFinalizer)
            {
                resource.Metadata.EnsureFinalizers().Add(CustomResourceDefinitionAttribute.DEFAULT_FINALIZER);
            }

            if (ns != null)
            {
                resource.Metadata.NamespaceProperty = ns;
            }
            resource.Metadata.Uid = uid;
            resource.Metadata.DeletionTimestamp = deletionTimeStamp;
            resource.Metadata.Generation = generation;

            resource.Spec = new TestableCustomResource.TestableSpec();
            resource.Status = new TestableCustomResource.TestableStatus();

            return resource;
        }

        protected void VerifyProcessedEvents(TestableController controller, params (TestableCustomResource resource, bool deleted)[] inputs) => Assert.Equal(inputs, controller.Invocations);
        protected void VerifyAddOrModifyIsCalledWith(TestableController controller,params TestableCustomResource[] inputs) => Assert.Equal(inputs, controller.Invocations_AddOrModify);
        protected void VerifyDeleteIsCalledWith(TestableController controller,params TestableCustomResource[] inputs) => Assert.Equal(inputs, controller.Invocations_Delete);
        protected void VerifyAddOrModifyIsNotCalled(TestableController controller) => Assert.Equal(new TestableCustomResource[] { }, controller.Invocations_AddOrModify);
        protected void VerifyDeleteIsNotCalled(TestableController controller) => Assert.Equal(new TestableCustomResource[] { }, controller.Invocations_Delete);        
    }
}
