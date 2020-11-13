using System.Dynamic;
using k8s.Models;
using k8s.Operators;

namespace k8s.Operators.Tests
{
    [CustomResourceDefinition("group", "v1", "resources")]
    public class TestableDynamicCustomResource : Operators.DynamicCustomResource
    {
        public TestableDynamicCustomResource() : base()
        {
            Metadata = new Models.V1ObjectMeta();
            Metadata.EnsureFinalizers().Add(CustomResourceDefinitionAttribute.DEFAULT_FINALIZER);
            Metadata.NamespaceProperty = "ns1";
            Metadata.Name = "resource1";
            Metadata.Generation = 1;
            Metadata.Uid = "id1";

            Spec = new ExpandoObject();
            Status = new ExpandoObject();
        }
    }
}
