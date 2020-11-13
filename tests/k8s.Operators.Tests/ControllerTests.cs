using System;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using k8s.Operators;
using Microsoft.AspNetCore.JsonPatch;
using Newtonsoft.Json;
using Xunit;
using Moq;

namespace k8s.Operators.Tests
{
    public class ControllerTests : BaseTests
    {
        private TestableController _controller;

        public ControllerTests()
        {
            _controller = new TestableController(OperatorConfiguration.Default,_client);
            _controller.RetryPolicy.InitialDelay = 1;
        }

        [Theory]
        [InlineData(WatchEventType.Added)]
        [InlineData(WatchEventType.Modified)]
        public async Task ProcessEventAsync_AddOrModifyIsCalled(WatchEventType eventType)
        {
            // Arrange
            var resource = CreateCustomResource();
            var resourceEvent = new CustomResourceEvent(eventType, resource);

            // Act
            await _controller.ProcessEventAsync(resourceEvent, DUMMY_TOKEN);

            // Assert
            VerifyAddOrModifyIsCalledWith(_controller, resource);
            VerifyDeleteIsNotCalled(_controller);
            VerifyNoOtherApiIsCalled();
        }

        [Theory]
        [InlineData(WatchEventType.Error)]
        [InlineData(WatchEventType.Deleted)]
        [InlineData(WatchEventType.Bookmark)]
        public async Task ProcessEventAsync_AddOrModifyIsNotCalled(WatchEventType eventType)
        {
            // Arrange
            var resource = CreateCustomResource();
            var resourceEvent = new CustomResourceEvent(eventType, resource);

            // Act
            await _controller.ProcessEventAsync(resourceEvent, DUMMY_TOKEN);

            // Assert
            VerifyAddOrModifyIsNotCalled(_controller);
            VerifyDeleteIsNotCalled(_controller);
            VerifyNoOtherApiIsCalled();
        }

        [Theory]
        [InlineData(WatchEventType.Added)]
        [InlineData(WatchEventType.Modified)]
        public async Task ProcessEventAsync_AddOrModifyIsNotCalledIfResourceIsAlreadyProcessed(WatchEventType eventType)
        {
            // Arrange
            var resource_v1 = CreateCustomResource(generation: 1);
            var resource_v2 = CreateCustomResource(generation: 2);

            // Act
            await _controller.ProcessEventAsync(new CustomResourceEvent(eventType, resource_v1), DUMMY_TOKEN);
            await _controller.ProcessEventAsync(new CustomResourceEvent(eventType, resource_v1), DUMMY_TOKEN);
            await _controller.ProcessEventAsync(new CustomResourceEvent(eventType, resource_v2), DUMMY_TOKEN);

            // Assert
            VerifyAddOrModifyIsCalledWith(_controller, resource_v1, resource_v2);
            VerifyDeleteIsNotCalled(_controller);
            VerifyNoOtherApiIsCalled();
        }

        [Fact]
        public async Task ProcessEventAsync_DeleteIsCalled()
        {
            // Arrange
            var resource = CreateCustomResource(deletionTimeStamp: DateTime.Now);
            var resourceEvent = new CustomResourceEvent(WatchEventType.Modified, resource);

            // Act
            await _controller.ProcessEventAsync(resourceEvent, DUMMY_TOKEN);

            // Assert
            VerifyAddOrModifyIsNotCalled(_controller);
            VerifyDeleteIsCalledWith(_controller, resource);
            VerifyReplaceIsCalled(resource, out TestableCustomResource updatedResource);
            Assert.Equal(0, updatedResource.Metadata.Finalizers.Count);
        }

        
        [Fact]
        public async Task ProcessEventAsync_UpdateResourceCallsReplaceApi()
        {
            // Arrange
            var resource = CreateCustomResource();

            // Act
            await _controller.Exposed_UpdateResourceAsync(resource, DUMMY_TOKEN);

            // Assert
            VerifyReplaceIsCalled(resource, out TestableCustomResource _);
        }

        [Fact]
        public async Task ProcessEventAsync_UpdateStatusCallsPatchApi()
        {
            // Arrange
            var resource = CreateCustomResource();
            resource.Spec.Property = "before";
            resource.Status.Property = "before";

            // Act
            await _controller.Exposed_UpdateStatusAsync(resource, DUMMY_TOKEN);

            // Assert
            var patch = new JsonPatchDocument<TestableCustomResource>().Replace(x => x.Status, resource.Status);
            VerifyPatchIsCalled(patch);
            VerifyNoOtherApiIsCalled();
        }

        [Theory]
        [InlineData(WatchEventType.Added)]
        [InlineData(WatchEventType.Modified)]
        public async Task ProcessEventAsync_MissingFinalizerIsAdded(WatchEventType eventType)
        {
            // Arrange
            var resource = CreateCustomResource(withFinalizer: false);
            var resourceEvent = new CustomResourceEvent(eventType, resource);

            // Act
            await _controller.ProcessEventAsync(resourceEvent, DUMMY_TOKEN);

            // Assert
            VerifyReplaceIsCalled(resource, out TestableCustomResource updatedResource);
            Assert.Equal(CustomResourceDefinitionAttribute.DEFAULT_FINALIZER, updatedResource.Metadata.Finalizers[0]);
        }

        [Theory]
        [InlineData(WatchEventType.Error, true)]
        [InlineData(WatchEventType.Error, false)]
        [InlineData(WatchEventType.Deleted, true)]
        [InlineData(WatchEventType.Deleted, false)]
        [InlineData(WatchEventType.Bookmark, true)]
        [InlineData(WatchEventType.Bookmark, false)]
        [InlineData(WatchEventType.Added, true)]
        [InlineData(WatchEventType.Modified, true)]
        public async Task ProcessEventAsync_FinalizerIsNotAdded(WatchEventType eventType, bool withFinalizer)
        {
            // Arrange
            var resource = CreateCustomResource(withFinalizer: withFinalizer);
            var resourceEvent = new CustomResourceEvent(eventType, resource);

            // Act
            await _controller.ProcessEventAsync(resourceEvent, DUMMY_TOKEN);

            // Assert
            VerifyNoOtherApiIsCalled();
        }

        [Theory]
        [InlineData(WatchEventType.Added, false, WatchEventType.Modified, false)]
        [InlineData(WatchEventType.Added, true, WatchEventType.Modified, false)]
        [InlineData(WatchEventType.Added, false, WatchEventType.Modified, true)]
        [InlineData(WatchEventType.Added, true, WatchEventType.Modified, true)]

        [InlineData(WatchEventType.Modified, false, WatchEventType.Added, false)]
        [InlineData(WatchEventType.Modified, true, WatchEventType.Added, false)]
        [InlineData(WatchEventType.Modified, false, WatchEventType.Added, true)]
        [InlineData(WatchEventType.Modified, true, WatchEventType.Added, true)]

        [InlineData(WatchEventType.Modified, false, WatchEventType.Modified, false)]
        [InlineData(WatchEventType.Modified, true, WatchEventType.Modified, false)]
        [InlineData(WatchEventType.Modified, false, WatchEventType.Modified, true)]
        [InlineData(WatchEventType.Modified, true, WatchEventType.Modified, true)]
        public void ProcessEventAsync_EventsForSameResourceAreProcessedSerially(WatchEventType eventType1, bool delete1, WatchEventType eventType2, bool delete2)
        {
            var resource_v1 = CreateCustomResource(generation: 1, deletionTimeStamp: delete1 ? DateTime.Now : (DateTime?) null);
            var resource_v2 = CreateCustomResource(generation: 2, deletionTimeStamp: delete2 ? DateTime.Now : (DateTime?) null);
            
            // Send 2 updates in a row for the same resource

            // Update #1
            var token1 = _controller.BlockNextEvent();
            var task1 = _controller.ProcessEventAsync(new CustomResourceEvent(eventType1, resource_v1), DUMMY_TOKEN);

            // Update #2
            var token2 = _controller.BlockNextEvent();
            var task2 = _controller.ProcessEventAsync(new CustomResourceEvent(eventType2, resource_v2), DUMMY_TOKEN);

            // Update #1 starts, update #2 is waiting
            VerifyCalledEvents(_controller, (resource_v1, delete1));

            // Update #1 ends, update #2 starts
            _controller.UnblockEvent(token1);
            VerifyCalledEvents(_controller, (resource_v1, delete1), (resource_v2, delete2));

            // Update #2 ends
            _controller.UnblockEvent(token2);
            Task.WaitAll(task2, task1);
        }

        [Theory]
        [InlineData(WatchEventType.Added, false, WatchEventType.Modified, false)]
        [InlineData(WatchEventType.Added, true, WatchEventType.Modified, false)]
        [InlineData(WatchEventType.Added, false, WatchEventType.Modified, true)]
        [InlineData(WatchEventType.Added, true, WatchEventType.Modified, true)]

        [InlineData(WatchEventType.Modified, false, WatchEventType.Added, false)]
        [InlineData(WatchEventType.Modified, true, WatchEventType.Added, false)]
        [InlineData(WatchEventType.Modified, false, WatchEventType.Added, true)]
        [InlineData(WatchEventType.Modified, true, WatchEventType.Added, true)]

        [InlineData(WatchEventType.Modified, false, WatchEventType.Modified, false)]
        [InlineData(WatchEventType.Modified, true, WatchEventType.Modified, false)]
        [InlineData(WatchEventType.Modified, false, WatchEventType.Modified, true)]
        [InlineData(WatchEventType.Modified, true, WatchEventType.Modified, true)]
        public void ProcessEventAsync_EventsForDifferentResourceAreProcessedConcurrently(WatchEventType eventType1, bool delete1, WatchEventType eventType2, bool delete2)
        {
            var resource1 = CreateCustomResource(uid: "1", deletionTimeStamp: delete1 ? DateTime.Now : (DateTime?) null);
            var resource2 = CreateCustomResource(uid: "2", deletionTimeStamp: delete2 ? DateTime.Now : (DateTime?) null);
            
            // Send 2 updates in a row for the different resources
            
            // Update #1
            var token1 = _controller.BlockNextEvent();
            var task1 = _controller.ProcessEventAsync(new CustomResourceEvent(eventType1, resource1), DUMMY_TOKEN);

            // Update #2
            var token2 = _controller.BlockNextEvent();
            var task2 = _controller.ProcessEventAsync(new CustomResourceEvent(eventType2, resource2), DUMMY_TOKEN);

            // Updates are processed concurrently
            VerifyCalledEvents(_controller, (resource1, delete1), (resource2, delete2));

            // Updates end
            _controller.UnblockEvent(token1);
            _controller.UnblockEvent(token2);
            Task.WaitAll(task2, task1);
        }

        [Theory]
        [InlineData(WatchEventType.Added, true)]
        [InlineData(WatchEventType.Modified, true)]
        [InlineData(WatchEventType.Added, false)]
        [InlineData(WatchEventType.Modified, false)]
        public async Task ProcessEventAsync_RetryOnFailure(WatchEventType eventType, bool delete)
        {
            // Arrange
            var resource = CreateCustomResource(deletionTimeStamp: delete ? DateTime.Now : (DateTime?) null);
            var resourceEvent = new CustomResourceEvent(eventType, resource);
            _controller.ThrowExceptionOnNextEvents(_controller.RetryPolicy.MaxAttempts); // ProcessEventAsync will fail n times, where n = MaxAttempts

            // Act
            await _controller.ProcessEventAsync(resourceEvent, DUMMY_TOKEN);

            // Assert
            VerifyCompletedEvents(_controller, (resource, deleteEvent: delete));
        }

        [Theory]
        [InlineData(WatchEventType.Added, true)]
        [InlineData(WatchEventType.Modified, true)]
        [InlineData(WatchEventType.Added, false)]
        [InlineData(WatchEventType.Modified, false)]
        public async Task ProcessEventAsync_NoRetryAfterMaxAttempts(WatchEventType eventType, bool delete)
        {
            // Arrange
            var resource = CreateCustomResource(deletionTimeStamp: delete ? DateTime.Now : (DateTime?) null);
            var resourceEvent = new CustomResourceEvent(eventType, resource);
            _controller.ThrowExceptionOnNextEvents(_controller.RetryPolicy.MaxAttempts + 1); // ProcessEventAsync will fail n + 1 times, where n = MaxAttempts

            // Act
            await _controller.ProcessEventAsync(resourceEvent, DUMMY_TOKEN);

            // Assert
            VerifyCompletedEvents(_controller);
        }

        [Theory]
        [InlineData(WatchEventType.Added, true)]
        [InlineData(WatchEventType.Modified, true)]
        [InlineData(WatchEventType.Added, false)]
        [InlineData(WatchEventType.Modified, false)]
        public void ProcessEventAsync_NoRetryIfNewEventForSameResourceIsQueued(WatchEventType eventType, bool delete)
        {
            // Arrange
            var resource_v1 = CreateCustomResource(generation: 1, deletionTimeStamp: delete ? DateTime.Now : (DateTime?) null);
            var resource_v2 = CreateCustomResource(generation: 2, deletionTimeStamp: delete ? DateTime.Now : (DateTime?) null);
            _controller.ThrowExceptionOnNextEvents(1);
            var block = _controller.BlockNextEvent();

            // Event #1, will block
            var task1 = _controller.ProcessEventAsync(new CustomResourceEvent(eventType, resource_v1), DUMMY_TOKEN);

            // Event #2, queued
            var task2 = _controller.ProcessEventAsync(new CustomResourceEvent(eventType, resource_v2), DUMMY_TOKEN);

            // Unblock #1, will throw an exception. But since #2 has arrived in the meantime, #1 will be discarded
            _controller.UnblockEvent(block);

            Task.WaitAll(task2, task1);

            // Assert
            VerifyCompletedEvents(_controller, (resource_v2, deleteEvent: delete));
        }

        [Theory]
        [InlineData(WatchEventType.Added, true)]
        [InlineData(WatchEventType.Modified, true)]
        [InlineData(WatchEventType.Added, false)]
        [InlineData(WatchEventType.Modified, false)]
        public void ProcessEventAsync_RetryIfNewEventForAnotherResourceIsQueued(WatchEventType eventType, bool delete)
        {
            // Arrange
            var resource1 = CreateCustomResource(uid: "1", deletionTimeStamp: delete ? DateTime.Now : (DateTime?) null);
            var resource2 = CreateCustomResource(uid: "2", deletionTimeStamp: delete ? DateTime.Now : (DateTime?) null);
            _controller.ThrowExceptionOnNextEvents(1);
            var block1 = _controller.BlockNextEvent();

            // Event #1, will block
            var task1 = _controller.ProcessEventAsync(new CustomResourceEvent(eventType, resource1), DUMMY_TOKEN);

            // Event #2, will be processed since it's a different resource
            var task2 = _controller.ProcessEventAsync(new CustomResourceEvent(eventType, resource2), DUMMY_TOKEN);

            // Unblock #1
            _controller.UnblockEvent(block1);

            Task.WaitAll(task2, task1);

            // Assert
            VerifyCompletedEvents(_controller, (resource1, deleteEvent: delete), (resource2, deleteEvent: delete));
        }

        [Theory]
        [InlineData(WatchEventType.Added, true)]
        [InlineData(WatchEventType.Modified, true)]
        [InlineData(WatchEventType.Added, false)]
        [InlineData(WatchEventType.Modified, false)]
        public void ProcessEventAsync_NoRetryIfCancelIsRequested(WatchEventType eventType, bool delete)
        {
            // Arrange
            var resource = CreateCustomResource(deletionTimeStamp: delete ? DateTime.Now : (DateTime?) null);
            var cts = new CancellationTokenSource();
            _controller.ThrowExceptionOnNextEvents(1);
            var block = _controller.BlockNextEvent();

            // Will block
            var task1 = _controller.ProcessEventAsync(new CustomResourceEvent(eventType, resource), cts.Token);

            // 
            cts.Cancel();

            // Unblock #1, will throw an exception. But since #2 has arrived in the meantime, #1 will be discarded
            _controller.UnblockEvent(block);

            Task.WaitAll(task1);

            // Assert
            VerifyCompletedEvents(_controller);
        }
        
        [Theory]
        [InlineData(WatchEventType.Added)]
        [InlineData(WatchEventType.Modified)]
        public async Task ProcessEventAsync_WithDynamicCustomResource(WatchEventType eventType)
        {            
            // Arrange
            var resource = new TestableDynamicCustomResource();
            resource.Spec.property = "desired";
            resource.Status.property = "actual";
            var resourceEvent = new CustomResourceEvent(eventType, resource);
            var controller = new TestableDynamicController();

            // Act
            await controller.ProcessEventAsync(resourceEvent, DUMMY_TOKEN);

            // Assert
            Assert.Equal(resource.Spec.property, resource.Status.property);
        }

        private void VerifyNoOtherApiIsCalled() => _clientMock.VerifyNoOtherCalls();

        private void VerifyReplaceIsCalled(object input, out TestableCustomResource updatedResource)
        {
            // Get the resource that has been passed to the API
            var resource = _clientMock.Invocations[0].Arguments[0] as TestableCustomResource;

            // Verify the API has been called once
            _clientMock.Verify(x => x.ReplaceNamespacedCustomObjectWithHttpMessagesAsync
            (
                input,
                "group",
                "v1",
                "ns1",
                "resources",
                "resource1",
                null,
                default(System.Threading.CancellationToken)
            ), Times.Once);

            updatedResource = resource;

            _clientMock.VerifyNoOtherCalls();
        }

        private void VerifyPatchIsCalled(IJsonPatchDocument expected)
        {
            // Verify the API has been called once
            _clientMock.Verify(x => x.PatchNamespacedCustomObjectStatusWithHttpMessagesAsync
            (
                It.IsAny<V1Patch>(),
                "group",
                "v1",
                "ns1",
                "resources",
                "resource1",
                null,
                default(System.Threading.CancellationToken)
            ), Times.Once);

            // Semantic equal assertion (JsonPatchDocument.Equals doesn't compare the content)
            var actual = (_clientMock.Invocations[0].Arguments[0] as V1Patch).Content as IJsonPatchDocument;
            Assert.Equal(JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(actual));

            _clientMock.VerifyNoOtherCalls();
        }
    }
}