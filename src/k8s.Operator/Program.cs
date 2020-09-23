using System;
using k8s;

namespace k8s.Operator
{
    class Program
    {
        static void Main(string[] args) 
        {
            // Load from the default kubeconfig on the machine.
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();

            // Use the config object to create a client.
            var client = new Kubernetes(config);

            var namespaces = client.ListNamespace();
            foreach (var ns in namespaces.Items) {
                Console.WriteLine(ns.Metadata.Name);
                var list = client.ListNamespacedPod(ns.Metadata.Name);
                foreach (var item in list.Items)
                {
                    Console.WriteLine(item.Metadata.Name);
                }
            }
        }
    }
}
