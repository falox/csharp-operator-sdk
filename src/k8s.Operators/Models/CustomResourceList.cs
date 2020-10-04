using System.Collections.Generic;
using k8s.Models;
using Newtonsoft.Json;

namespace k8s.Operators
{
    /// <summary>
    /// Represents a Kubernetes list of custom resources of type T
    /// </summary>
    public abstract class CustomResourceList<T> : KubernetesObject where T : CustomResource
    {
        [JsonProperty("metadata")]
        public V1ListMeta Metadata { get; set; }

        [JsonProperty("items")]
        public List<T> Items { get; set; }
    }
}