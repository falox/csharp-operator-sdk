using k8s.Operators;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace k8s.Operators.Samples.Dynamic
{
    [CustomResourceDefinition("csharp-operator.example.com", "v1", "myresources")]
    public class MyDynamicResource : DynamicCustomResource
    {
        public override string ToString()
        {
            return $"{Metadata.NamespaceProperty}/{Metadata.Name} (gen: {Metadata.Generation}), Spec: {JsonConvert.SerializeObject(Spec)} Status: {JsonConvert.SerializeObject(Status ?? new object())}";
        }
    }
}