using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.Rest;

namespace k8s.Operators.Logging
{
    /// <summary>
    /// A console tracer for the ServiceClientTracing service used by the Kubernetes C# client
    /// </summary>
    /// <see cref="http://azure.github.io/autorest/client/tracing.html#custom-tracing"/>
    public class ConsoleTracingInterceptor : IServiceClientTracingInterceptor
    {
        public void Information(string message)
        {
            Console.WriteLine(message);
        }

        public void TraceError(string invocationId, Exception exception)
        {
            Console.WriteLine($"invocationId: {invocationId}, exception: {exception}");
        }

        public void ReceiveResponse(string invocationId, HttpResponseMessage response)
        {
            Console.WriteLine($"invocationId: {invocationId}\r\nresponse: {(response == null ? string.Empty : response.AsFormattedString())}");
        }

        public void SendRequest(string invocationId, HttpRequestMessage request)
        {
            Console.WriteLine($"invocationId: {invocationId}\r\nrequest: {(request == null ? string.Empty : request.AsFormattedString())}");
        }

        public void Configuration(string source, string name, string value)
        {
            Console.WriteLine($"Configuration: source={source}, name={name}, value={value}");
        }

        public void EnterMethod(string invocationId, object instance, string method, IDictionary<string, object> parameters)
        {
            Console.WriteLine($"invocationId: {invocationId}\r\ninstance: {instance}\r\nmethod: {method}\r\nparameters: {parameters.AsFormattedString()}");
        }

        public void ExitMethod(string invocationId, object returnValue)
        {
            Console.WriteLine($"invocationId: {invocationId}, return value: {(returnValue == null ? string.Empty : returnValue.ToString())}");
        }
    }
}