# Monq.Core.MicroserviceAddressing

`Monq.Core.MicroserviceAddressing` provides shared address resolution policies for
MONQ microservices. It creates logical addresses and indicates whether a client must
connect directly or enable client-side service discovery. DNS lookup, HTTP endpoint
selection, and gRPC balancing remain the responsibility of the corresponding client
libraries.

## Built-in conventions

| Strategy | HTTP | gRPC |
|---|---|---|
| ClusterIP | `http://{application}.{namespace}.svc.cluster.local:80` | `http://{application}.{namespace}.svc.cluster.local:5006` |
| Service Discovery | `http://{application}-http.{namespace}.svc.cluster.local:5005` | `dns:///{application}-grpc.{namespace}.svc.cluster.local:5006` |
| Fixed | The supplied absolute URI | The supplied absolute URI |

The namespace is derived from `IHostEnvironment.EnvironmentName` and normalized to
lowercase. Pass a port to override a strategy default.

## Registration

The built-in fallback is Kubernetes ClusterIP:

```csharp
builder.Services.AddMicroserviceAddressing();
```

HTTP and gRPC can use different policies:

```csharp
builder.Services
    .UseHttpMicroserviceAddressResolver(
        new KubernetesClusterIpAddressResolver())
    .UseGrpcMicroserviceAddressResolver(
        new KubernetesServiceDiscoveryAddressResolver());
```

Use one common resolver when a library should provide protocol-specific defaults
that an application can replace with a single common override:

```csharp
builder.Services.UseMicroserviceAddressResolver(
    new ProtocolMicroserviceAddressResolver(
        httpResolver: new KubernetesClusterIpAddressResolver(),
        grpcResolver: new KubernetesServiceDiscoveryAddressResolver()));
```

A library that provides a fallback policy can call `TryUseMicroserviceAddressResolver`
instead: it selects the default resolver only when none was selected before, so an
earlier application selection is preserved.

Registrations are observed by the address provider when a client resolves an address,
so the last registration for a scope always wins. In console and worker applications
that build the host with a fluent chain, register resolvers inside `ConfigureServices`
to observe the final host configuration and environment:

```csharp
Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) => services.UseMicroserviceAddressResolver(
        new ProductAddressResolver(context.Configuration)));
```

Resolve an address from another library or application component:

```csharp
public sealed class EndpointConsumer(IMicroserviceAddressProvider addresses)
{
    public Uri Resolve(string applicationName, int port) =>
        addresses.Resolve(
            applicationName,
            MicroserviceProtocol.Http,
            port).Address;
}
```

Resolver precedence is:

1. Resolver supplied for a particular client.
2. HTTP or gRPC resolver.
3. Default resolver.
4. Built-in Kubernetes ClusterIP resolver.

## Custom policy

Implement `IMicroserviceAddressResolver` and replace only the required scope:

```csharp
public sealed class ProductAddressResolver : IMicroserviceAddressResolver
{
    public ResolvedMicroserviceAddress Resolve(MicroserviceAddressContext context)
    {
        var address = context.Environment.IsDevelopment()
            ? new Uri(context.Configuration["BaseUri"]!)
            : new Uri(
                $"http://{context.TargetApplicationName}.custom.svc.cluster.local:{context.PortOverride ?? 80}");

        return new(address);
    }
}

builder.Services.UseHttpMicroserviceAddressResolver(
    new ProductAddressResolver());
```

The selected resolver instance is shared by client registration and the runtime
`IMicroserviceAddressProvider`.
