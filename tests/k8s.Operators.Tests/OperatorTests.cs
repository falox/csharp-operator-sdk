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
            _operator = new TestableOperator(OperatorConfiguration.Default, _client, _loggerFactory);
        }

        [Fact]
        public void AddController_ThrowsExceptionIfControllerIsNull()
        {
            Assert.Throws<ValidationException>(() => _operator.AddController<TestableCustomResource>(null));
        }

        [Fact]
        public void Dispose_ExecutesOnlyOnce()
        {
            _operator.Dispose();
            _operator.Dispose();

            Assert.False(_operator.IsRunning);
            Assert.False(_operator.IsDisposing);
            Assert.True(_operator.IsDisposed);
            Assert.Equal(1, _operator.DisposeInvocationCount);
        }

        [Fact]
        public void Stop_ExecutesOnlyOnce()
        {
            _operator.Stop();
            _operator.Stop();

            Assert.False(_operator.IsRunning);
            Assert.False(_operator.IsDisposing);
            Assert.True(_operator.IsDisposed);
            Assert.Equal(1, _operator.DisposeInvocationCount);
        }

        [Fact]
        public async Task StartAsync_CallsDisposeAndStopIfNoControllersArePresent()
        {
            await _operator.StartAsync();

            Assert.False(_operator.IsRunning);
            Assert.False(_operator.IsDisposing);
            Assert.True(_operator.IsDisposed);
        }

        [Fact]
        public void StartAsync_ThrowsExceptionIfDisposed()
        {
            _operator.AddController<TestableCustomResource>(new TestableController());
            _operator.Dispose();

            Assert.True(_operator.IsDisposed);
            Assert.False(_operator.IsDisposing);
            Assert.ThrowsAsync<ObjectDisposedException>(() => _operator.StartAsync());
        }

        [Fact]
        public void StartAsync_ThrowsExceptionIfStopped()
        {
            _operator.AddController<TestableCustomResource>(new TestableController());
            _operator.Stop();

            Assert.True(_operator.IsDisposed);
            Assert.False(_operator.IsDisposing);
            Assert.ThrowsAsync<ObjectDisposedException>(() => _operator.StartAsync());
        }

        [Fact]
        public async Task AddControllerOfType_CreatesAndAddController()
        {
            // Arrange
            var resource = CreateCustomResource();

            // Act
            var controller = (TestableController) _operator.AddControllerOfType<TestableController>();
            
            // Assert
            var task =_operator.StartAsync();
            _operator.Exposed_OnIncomingEvent(WatchEventType.Added, resource);
            _operator.Stop(); await task;
            VerifyAddOrModifyIsCalledWith(controller, resource);
        }

        [Theory]
        [InlineData(WatchEventType.Added)]
        [InlineData(WatchEventType.Modified)]
        public async Task AddController_EventIsDispatchedToGenericController(WatchEventType eventType)
        {
            // Arrange
            var resource = CreateCustomResource();
            var genericController = new TestableController(_client, _loggerFactory);
            var namespaceController = new TestableController(_client, _loggerFactory);

            // Act
            _operator.AddController(genericController, ""); // all namespaces
            _operator.AddController(namespaceController, "namespace1");
            
            // Assert
            var task =_operator.StartAsync();
            _operator.Exposed_OnIncomingEvent(eventType, resource);
            _operator.Stop(); await task;
            VerifyAddOrModifyIsCalledWith(genericController, resource);
            VerifyAddOrModifyIsNotCalled(namespaceController);
        }

        [Theory]
        [InlineData(WatchEventType.Added)]
        [InlineData(WatchEventType.Modified)]
        public async Task AddController_WatchDefaultNamespaceIfNotSpecified(WatchEventType eventType)
        {
            // Arrange
            var resource = CreateCustomResource(ns: "default");
            var genericController = new TestableController(_client, _loggerFactory);
            var namespaceController = new TestableController(_client, _loggerFactory);

            // Act
            _operator.AddController(genericController);
            
            // Assert
            var task =_operator.StartAsync();
            _operator.Exposed_OnIncomingEvent(eventType, resource);
            _operator.Stop(); await task;
            VerifyAddOrModifyIsCalledWith(genericController, resource);
            VerifyAddOrModifyIsNotCalled(namespaceController);
        }

        [Theory]
        [InlineData(WatchEventType.Added)]
        [InlineData(WatchEventType.Modified)]
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
        [InlineData(WatchEventType.Added, "")]
        [InlineData(WatchEventType.Modified, "")]
        [InlineData(WatchEventType.Added, null)]
        [InlineData(WatchEventType.Modified, null)]
        public async Task OnIncomingEvent_EventsAreDispatchedToAssociatedControllers(WatchEventType eventType, string allNamespaceVariant)
        {
            // Arrange
            var resource1 = CreateCustomResource(ns: "namespace1");
            var resource2 = CreateCustomResource(ns: "namespace2");
            var genericController = new TestableController(_client);
            var namespaceController = new TestableController(_client);
            _operator.AddController(genericController, allNamespaceVariant); // all namespaces
            _operator.AddController(namespaceController, "namespace1");
            _operator.AddController(namespaceController, "namespace3");
            var task =_operator.StartAsync();

            // Act
            _operator.Exposed_OnIncomingEvent(eventType, resource1);
            _operator.Exposed_OnIncomingEvent(eventType, resource2);
            
            // Assert
            _operator.Stop(); await task;
            VerifyAddOrModifyIsCalledWith(namespaceController, resource1);
            VerifyAddOrModifyIsCalledWith(genericController, resource2);
        }

        [Theory]
        [InlineData(WatchEventType.Error)]
        [InlineData(WatchEventType.Deleted)]
        [InlineData(WatchEventType.Bookmark)]
        public async Task OnIncomingEvent_EventsAreDispatchedAndIgnored(WatchEventType eventType)
        {
            // Arrange
            var resource = CreateCustomResource(ns: "namespace1");
            var controller = new TestableController(_client);
            _operator.AddController(controller, "namespace1");
            var task =_operator.StartAsync();

            // Act
            _operator.Exposed_OnIncomingEvent(eventType, resource);
            
            // Assert
            _operator.Stop(); await task;
            VerifyAddOrModifyIsNotCalled(controller);
            VerifyDeleteIsNotCalled(controller);
        }
    }
}