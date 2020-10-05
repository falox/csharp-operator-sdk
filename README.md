![.NET Core](https://github.com/falox/csharp-operator-sdk/workflows/.NET%20Core/badge.svg?branch=master)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/k8s.Operators)

# C# Operator SDK

The C# Operator SDK implements a framework to build [Kubernets operators](https://kubernetes.io/docs/concepts/extend-kubernetes/operator/) with C# and .NET Core.

## Usage

In the `/samples/basic` directory you find a [sample operator](./samples/basic/README.md) that simulates the interaction with an external service and can be used as a template for real-world operators. 

[Follow the instructions](./samples/basic/README.md) to run it locally and deploy it to Kubernetes.

## Roadmap

- v1.0.0 (2020 Q4)
    - Retry and auto-recovery on watching and event handling failures
    - [Bookmark event](https://kubernetes.io/docs/reference/using-api/api-concepts/#watch-bookmarks) support
    - Configurable policy for ResourceChangeTracker (flag skipSameGenerationEvents)
- v1.1.0 (2021)
    - Dynamic custom resource sample
    - Dependency injection sample
    - Single-instance check
    - Leader election support

## References

- Jason Dobies and Joshua Wood, [Kubernetes Operators](https://www.redhat.com/cms/managed-files/cl-oreilly-kubernetes-operators-ebook-f21452-202001-en_2.pdf), O'Reilly, 2020
- Radu Matei, [Writing controllers for Kubernetes CRDs with C#](https://radu-matei.com/blog/kubernetes-controller-csharp/), 2019
