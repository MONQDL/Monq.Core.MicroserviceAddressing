namespace Monq.Core.MicroserviceAddressing.Addressing;

/// <summary>
/// Resolves addresses of Kubernetes headless Services for client-side endpoint discovery.
/// </summary>
public sealed class KubernetesServiceDiscoveryAddressResolver : IMicroserviceAddressResolver
{
    /// <inheritdoc />
    public ResolvedMicroserviceAddress Resolve(MicroserviceAddressContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(context.TargetApplicationName))
            throw new ArgumentException(
                "The target application name cannot be empty.",
                nameof(context.TargetApplicationName));

        var namespaceName = context.Environment.EnvironmentName.ToLowerInvariant();
        var address = context.Protocol switch
        {
            MicroserviceProtocol.Http => new Uri(
                $"http://{context.TargetApplicationName}-http.{namespaceName}.svc.cluster.local:{context.PortOverride ?? 5005}"),
            MicroserviceProtocol.Grpc => new Uri(
                $"dns:///{context.TargetApplicationName}-grpc.{namespaceName}.svc.cluster.local:{context.PortOverride ?? 5006}"),
            _ => throw new ArgumentOutOfRangeException(nameof(context), context.Protocol, "Unsupported protocol."),
        };

        return new(address, EndpointResolutionMode.ServiceDiscovery);
    }
}
