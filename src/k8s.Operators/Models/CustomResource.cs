using System;
using k8s.Models;
using Newtonsoft.Json;

namespace k8s.Operators
{
    /// <summary>
    /// Represents a Kubernetes custom resource
    /// </summary>
    public abstract class CustomResource : KubernetesObject, IKubernetesObject<V1ObjectMeta>
    {
        [JsonProperty("metadata")]
        public V1ObjectMeta Metadata { get; set; }

        public override string ToString()
        {
            return $"{Metadata.NamespaceProperty}/{Metadata.Name} (gen: {Metadata.Generation}, uid: {Metadata.Uid})";
        }
    }

    /// <summary>
    /// Represents a Kubernetes custom resource that has a spec
    /// </summary>
    public abstract class CustomResource<TSpec> : CustomResource, ISpec<TSpec>
    {
        [JsonProperty("spec")]
        public TSpec Spec { get; set; }
    }

    /// <summary>
    /// Represents a Kubernetes custom resource that has a spec and status
    /// </summary>
    public abstract class CustomResource<TSpec, TStatus> : CustomResource<TSpec>, IStatus, IStatus<TStatus>
    {
        [JsonProperty("status")]
        public TStatus Status { get; set; }

        object IStatus.Status { get => Status; set => Status = (TStatus) value; }
    }
}