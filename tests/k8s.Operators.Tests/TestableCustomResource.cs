using k8s.Operators;

namespace k8s.Operators.Tests
{
    [CustomResourceDefinition("group", "v1", "resources")]
    public class TestableCustomResource : Operators.CustomResource<TestableCustomResource.TestableSpec, TestableCustomResource.TestableStatus>
    {
        public TestableCustomResource()
        {
            Metadata = new Models.V1ObjectMeta();
            Metadata.NamespaceProperty = "ns1";
            Metadata.Name = "resource1";
            Metadata.Generation = 1;
            Metadata.Uid = "id1";
        }

        public class TestableSpec
        {
            public string Property { get; set; }
        }

        public class TestableStatus
        {
            public string Property { get; set; }
        }
    }
}
