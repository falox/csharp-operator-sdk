using Newtonsoft.Json;

namespace k8s.Operators.Samples.Basic
{
    [CustomResourceDefinition("csharp-operator.example.com", "v1", "myresources")]
    public class MyResource : CustomResource<MyResource.MyResourceSpec, MyResource.MyResourceStatus>
    {
        public class MyResourceSpec
        {
            [JsonProperty("desiredProperty")]
            public int Desired { get; set; }
        }

        public class MyResourceStatus
        {
            [JsonProperty("actualProperty")]
            public int Actual { get; set; }
        }

        public override string ToString()
        {
            return $"{Metadata.NamespaceProperty}/{Metadata.Name} (gen: {Metadata.Generation}), Spec: {Spec.Desired} Status: {Status?.Actual}";
        }
    }
}