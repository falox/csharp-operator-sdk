namespace k8s.Operators
{
    /// <summary>
    /// Kubernetes custom resource that exposes status
    /// </summary>
    public interface IStatus
    {
        object Status { get; set; }
    }
}