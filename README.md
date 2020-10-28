[![.NET Core](https://github.com/falox/csharp-operator-sdk/workflows/.NET%20Core/badge.svg?branch=master)](https://github.com/falox/csharp-operator-sdk/actions?query=workflow%3A%22.NET+Core%22)
[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/k8s.Operators)](https://www.nuget.org/packages/k8s.Operators)
[![Coverage Status](https://coveralls.io/repos/github/falox/csharp-operator-sdk/badge.svg?branch=master)](https://coveralls.io/github/falox/csharp-operator-sdk?branch=master)

# C# Operator SDK

The C# Operator SDK is a framework to build [Kubernetes operators](https://kubernetes.io/docs/concepts/extend-kubernetes/operator/) with C# and .NET Core.

## Features

- Easy custom resource and controller definition as C# classes
- Custom resource event watchers at namespace and cluster scope
- [Configurable retry-on-failure](https://github.com/falox/csharp-operator-sdk/blob/f989ab3ad5fdf322f681c863052338c982680bc5/samples/basic/deploy/operator.yaml#L27) policy
- Smart concurrent event queues (inspired by [Container Solution](https://blog.container-solutions.com/a-deep-dive-into-the-java-operator-sdk)'s article)
- Kubernetes [graceful termination policy](https://github.com/falox/csharp-operator-sdk/blob/f989ab3ad5fdf322f681c863052338c982680bc5/samples/basic/Program.cs#L89) support

## Usage

Setup a new [.NET Core 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) project and add the [C# Operator SDK package](https://www.nuget.org/packages/k8s.Operators):

```bash
dotnet new console
dotnet add package k8s.Operators
```

Assuming that you have already added a custom resource definition for `MyResource` in your Kubernetes cluster, define a class deriving from `CustomResource` for the custom resource schema:

```csharp
// Set the CRD attributes
[CustomResourceDefinition("example.com", "v1", "myresources")]
public class MyResource : CustomResource<MyResource.MyResourceSpec, MyResource.MyResourceStatus>
{
    // Define spec
    public class MyResourceSpec
    {
        public int property1 { get; set; }
        // :
    }

    // Define status
    public class MyResourceStatus
    {
        public int property2 { get; set; }
        // :
    }
}
```

Define a class deriving from `Controller` for the controller logic:

```csharp
public class MyResourceController : Controller<MyResource>
{
    public MyResourceController(OperatorConfiguration configuration, IKubernetes client, ILoggerFactory loggerFactory = null) 
        : base(configuration, client, loggerFactory)
    {
    }

    protected override async Task AddOrModifyAsync(MyResource resource, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Add/Modify {resource}");
        // :
        // Handle Add/Modify event
    }

    protected override async Task DeleteAsync(MyResource resource, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Delete {resource}");
        // :
        // Handle Delete event
    }
}
```

Setup the operator in `Main()`:

```csharp
static async Task<int> Main(string[] args)
{
    // Create the Kubernetes client
    using var client = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile());

    // Setup the operator
    var @operator = new Operator(OperatorConfiguration.Default, client);
    @operator.AddControllerOfType<MyResourceController>();

    // Start the operator
    return await @operator.StartAsync();
}
```

> Curiosity: Since `operator` is a reserved keyword in C#, it has been escaped with `@operator`.

Start the operator with:

```bash
dotnet run
```

In the `/samples/basic` directory you find a [sample operator](./samples/basic/README.md) that simulates the interaction with an external service and can be used as a template for real-world operators. 

[Follow the instructions](./samples/basic/README.md) to run it locally and deploy it to Kubernetes.

## Compiling the source code

```bash
git clone https://github.com/falox/csharp-operator-sdk.git
cd csharp-operator-sdk
dotnet restore
dotnet build
```

Running the tests:

```bash
dotnet test
```

## Upcoming features

- Configurable policy for ResourceChangeTracker (flag skipSameGenerationEvents)
- Leader election support
- Dynamic custom resource sample
- Single-instance check

## References

- Jason Dobies and Joshua Wood, [Kubernetes Operators](https://www.oreilly.com/library/view/kubernetes-operators/9781492048039/), O'Reilly, 2020
- Radu Matei, [Writing controllers for Kubernetes CRDs with C#](https://radu-matei.com/blog/kubernetes-controller-csharp/), 2019
