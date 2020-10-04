# C# Basic Kubernetes Operator

The *Basic Operator* handles the `MyResource` custom resource. The operator simulates the interaction with an external service and can be used as a template for real-world operators.

Once the operator detects an added/modified event, it waits for 5 seconds and:

- Adds a custom annotation `custom-key` in the object's `metadata.annotation`
- Updates the `status.actualProperty` to match the `spec.desiredProperty`

See the implementation of `MyResourceController.cs` for more details.

## Prerequisites

- [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1)
- [Kubectl](https://kubernetes.io/docs/tasks/tools/install-kubectl/)
- [Kubernetes](https://kubernetes.io/docs/setup/)


## Running locally

1. Add the custom resource definition to your Kubernetes cluster:

    ```bash
    $ cd csharp-operator-sdk/samples/basic
    $ kubectl apply -f ./deploy/crds/crd.yaml
    ```

2. Compile and run the operator:
    ```bash
    $ dotnet build
    $ dotnet run
    ```

    The operator will connect to the Kubernetes cluster and will start watching for events. You'll see something similar to:

    ```
    <6>k8s.Operators.Operator[0] Start operator
    ```

3. In another terminal, create a new `MyResource` object:

    ```bash
    $ kubectl apply -f ./deploy/crds/cr.yaml
    ```

    The operator will detect the new object and after 5 seconds will update the *status* to match the desired *spec*:

    ```
    <6>k8s.Operators.Controller[0] Begin AddOrModify default/mr1 (gen: 1), Spec: 1 Status: 
    <6>k8s.Operators.Controller[0] End AddOrModify default/mr1 (gen: 1), Spec: 1 Status: 1
    ```

4. Edit the resource and change the `spec.desiredProperty` to `2`:

    ```bash
    $ kubectl edit myresources mr1
    ```

    ```yaml
    apiVersion: csharp-operator.example.com/v1
    kind: MyResource
    :
    spec:
      desiredProperty: 2
      :
    ```

    The operator will detect the change and will align again the *status*:

    ```
    <6>k8s.Operators.Controller[0] Begin AddOrModify default/mr1 (gen: 2), Spec: 2 Status: 1
    <6>k8s.Operators.Controller[0] End AddOrModify default/mr1 (gen: 2), Spec: 2 Status: 2
    ```

5. Delete the resource:

    ```bash
    $ kubectl delete myresources mr1
    ```

    The operator will simulate the deletion of the resource:

    ```
    <6>k8s.Operators.Controller[0] Begin Delete default/mr1 (gen: 3), Spec: 2 Status: 2
    <6>k8s.Operators.Controller[0] End Delete default/mr1 (gen: 3), Spec: 2 Status: 2
    ```

6. Shutdown the operator with CTRL+C or by sending a SIGTERM signal with:

    ```bash
    kill $(ps aux | grep '[k]8s.Operators.Samples.Basic' | awk '{print $2}')
    ```

    The operator will gracefully shutdown:

    ```
    <6>k8s.Operators.Operator[0] Stop operator
    <6>k8s.Operators.Operator[0] Disposing operator
    ```

## Deploy the operator in Kubernetes

1. If you are running [Minikube](https://kubernetes.io/docs/setup/learning-environment/minikube/), point your shell to minikube's docker-daemon:

    ```bash
    $ eval $(minikube -p minikube docker-env)
    ```

2. Create the docker image:

    ```bash
    $ cd csharp-operator-sdk
    $ docker build -t csharp-basic-operator -f samples/basic/Dockerfile .
    ```

3. Deploy the operator 

    ```bash
    $ cd samples/basic/
    $ kubectl apply -f ./deploy/service_account.yaml
    $ kubectl apply -f ./deploy/role.yaml
    $ kubectl apply -f ./deploy/role_binding.yaml
    $ kubectl apply -f ./deploy/operator.yaml
    ```