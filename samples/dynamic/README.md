# C# Dynamic Kubernetes Operator

The *Dynamic Operator* is a variant of the [*Basic Operator*](../basic/README.md).

The `MyResource` class of the Basic Operator has been replaced with the `MyDynamicResource` class:

```csharp
[CustomResourceDefinition("csharp-operator.example.com", "v1", "myresources")]
public class MyDynamicResource : DynamicCustomResource
{
}
```

A `DynamicCustomResource` doesn't force you to strongly define the schema of `Spec` and `Status` in advance ([pros and cons](https://docs.microsoft.com/en-us/archive/msdn-magazine/2011/february/msdn-magazine-dynamic-net-understanding-the-dynamic-keyword-in-csharp-4)), and you can read and write any property without errors at compile time:

```csharp
string x = resource.Spec.foo;
resource.Status.bar = 123;
```

You can run and deploy the Dynamic Operator by following the same [instructions of the Basic Operator](../basic/README.md). Just replace `basic` with `dynamic` in the paths and commands.