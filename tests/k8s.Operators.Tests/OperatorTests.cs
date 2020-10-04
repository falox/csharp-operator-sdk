using System;
using System.Threading.Tasks;
using Microsoft.Rest;
using Xunit;
using Moq;
using k8s.Operators;

namespace k8s.Operators.Tests
{
    public class OperatorTests : BaseTests
    {
        private TestableOperator _operator;

        public OperatorTests()
        {
            _operator = new TestableOperator(_client);
        }

        [Fact]
        public void AddController_ThrowsExceptionIfControllerIsNull()
        {
            Assert.Throws<ValidationException>(() => _operator.AddController<TestableCustomResource>(null));
        }

        [Fact]
        public void StartAsync_CallsDisposeAndStopIfNoControllersArePresent()
        {
            _operator.StartAsync();

            Assert.False(_operator.IsRunning);
            Assert.True(_operator.IsDisposed);
        }

        [Fact]
        public void StartAsync_ThrowsExceptionIfDisposed()
        {
            _operator.AddController<TestableCustomResource>(new TestableController()).Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(() => _operator.StartAsync());
        }

        [Fact]
        public void StartAsync_ThrowsExceptionIfStopped()
        {
            _operator.AddController<TestableCustomResource>(new TestableController()).Stop();

            Assert.ThrowsAsync<ObjectDisposedException>(() => _operator.StartAsync());
        }

        [Theory]
        [InlineData(WatchEventType.Added)]
        [InlineData(WatchEventType.Modified)]
        [InlineData(WatchEventType.Bookmark)]
        public async Task OnIncomingEvent_EventIsDispatchedToGenericController(WatchEventType eventType)
        {
            // Arrange
            var resource = CreateCustomResource();
            var genericController = new TestableController(_client);
            var namespaceController = new TestableController(_client);
            _operator.AddController(genericController); // all namespaces
            _operator.AddController(namespaceController, "namespace1");
            var task =_operator.StartAsync();

            // Act
            _operator.Exposed_OnIncomingEvent(eventType, resource);
            
            // Assert
            _operator.Stop(); await task;
            VerifyAddOrModifyIsCalledWith(genericController, resource);
            VerifyAddOrModifyIsNotCalled(namespaceController);
        }

        [Theory]
        [InlineData(WatchEventType.Added)]
        [InlineData(WatchEventType.Modified)]
        [InlineData(WatchEventType.Bookmark)]
        public async Task OnIncomingEvent_EventIsDiscardedIfNoControllerIsAssociated(WatchEventType eventType)
        {
            // Arrange
            var resource = CreateCustomResource(ns: "namespace");
            var namespaceController = new TestableController(_client);
            _operator.AddController(namespaceController, "another-namespace");
            var task =_operator.StartAsync();

            // Act
            _operator.Exposed_OnIncomingEvent(eventType, resource);
            
            // Assert
            _operator.Stop(); await task;
            VerifyAddOrModifyIsNotCalled(namespaceController);
        }

        [Theory]
        [InlineData(WatchEventType.Added)]
        [InlineData(WatchEventType.Modified)]
        [InlineData(WatchEventType.Bookmark)]
        public async Task OnIncomingEvent_EventIsDispatchedToSpecificController(WatchEventType eventType)
        {
            // Arrange
            var resource = CreateCustomResource(ns: "namespace1");
            var genericController = new TestableController(_client);
            var namespaceController1 = new TestableController(_client);
            var namespaceController2 = new TestableController(_client);
            _operator.AddController(genericController); // all namespaces
            _operator.AddController(namespaceController1, "namespace1");
            _operator.AddController(namespaceController2, "namespace2");
            var task =_operator.StartAsync();

            // Act
            _operator.Exposed_OnIncomingEvent(eventType, resource);
            
            // Assert
            _operator.Stop(); await task;
            VerifyAddOrModifyIsCalledWith(namespaceController1, resource);
            VerifyAddOrModifyIsNotCalled(namespaceController2);
            VerifyAddOrModifyIsNotCalled(genericController);
        }

        [Theory]
        [InlineData(WatchEventType.Added)]
        [InlineData(WatchEventType.Modified)]
        [InlineData(WatchEventType.Bookmark)]
        public async Task OnIncomingEvent_EventsAreDispatchedToAssociatedControllers(WatchEventType eventType)
        {
            // Arrange
            var resource1 = CreateCustomResource(ns: "namespace1");
            var resource2 = CreateCustomResource(ns: "namespace2");
            var genericController = new TestableController(_client);
            var namespaceController = new TestableController(_client);
            _operator.AddController(genericController); // all namespaces
            _operator.AddController(namespaceController, "namespace1", "namespace3");
            var task =_operator.StartAsync();

            // Act
            _operator.Exposed_OnIncomingEvent(eventType, resource1);
            _operator.Exposed_OnIncomingEvent(eventType, resource2);
            
            // Assert
            _operator.Stop(); await task;
            VerifyAddOrModifyIsCalledWith(namespaceController, resource1);
            VerifyAddOrModifyIsCalledWith(genericController, resource2);
        }
    }
}
