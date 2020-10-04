using System;

namespace k8s.Operators
{
    /// <summary>
    /// Describe the essential custom resource definition attributes used by the Controller
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class CustomResourceDefinitionAttribute : Attribute
    {
        public const string DEFAULT_FINALIZER = "operator.default.finalizer";

        public CustomResourceDefinitionAttribute(string group, string version, string plural)
        {
            Group = group;
            Version = version;
            Plural = plural;
        }

        public string Group { get; private set; }
        public string Version { get; private set; }
        public string Plural { get; private set; }
        public string Finalizer { get; set; } = DEFAULT_FINALIZER;
    }
}