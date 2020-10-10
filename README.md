[![.NET Core](https://github.com/falox/csharp-operator-sdk/workflows/.NET%20Core/badge.svg?branch=master)](https://github.com/falox/csharp-operator-sdk/actions?query=workflow%3A%22.NET+Core%22)
[![Coverage Status](https://coveralls.io/repos/github/falox/csharp-operator-sdk/badge.svg?branch=master)](https://coveralls.io/github/falox/csharp-operator-sdk?branch=master)
[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/k8s.Operators)](https://www.nuget.org/packages/k8s.Operators)

# C# Operator SDK

The C# Operator SDK implements a framework to build [Kubernetes operators](https://kubernetes.io/docs/concepts/extend-kubernetes/operator/) with C# and .NET Core.

## Features

- Custom resource watches and event handling (namespace and cluster level)
- Configurable controller retry-on-failure policy
- Kubernetes graceful termination policy
- Smart event queues (inspired by [Java Operator SDK](https://blog.container-solutions.com/a-deep-dive-into-the-java-operator-sdk))

## Usage

In the `/samples/basic` directory you find a [sample operator](./samples/basic/README.md) that simulates the interaction with an external service and can be used as a template for real-world operators. 

[Follow the instructions](./samples/basic/README.md) to run it locally and deploy it to Kubernetes.

## Roadmap

- Configurable policy for ResourceChangeTracker (flag skipSameGenerationEvents)
- [Bookmark event](https://kubernetes.io/docs/reference/using-api/api-concepts/#watch-bookmarks) support
- Dynamic custom resource sample
- Single-instance check
- Leader election support

## References

- Jason Dobies and Joshua Wood, [Kubernetes Operators](https://www.oreilly.com/library/view/kubernetes-operators/9781492048039/), O'Reilly, 2020
- Radu Matei, [Writing controllers for Kubernetes CRDs with C#](https://radu-matei.com/blog/kubernetes-controller-csharp/), 2019
