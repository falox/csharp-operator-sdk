![.NET Core](https://github.com/falox/csharp-operator-sdk/workflows/.NET%20Core/badge.svg?branch=master)

# C# Operator SDK

SDK for building Kubernetes operators in C# on .NET Core. 

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
