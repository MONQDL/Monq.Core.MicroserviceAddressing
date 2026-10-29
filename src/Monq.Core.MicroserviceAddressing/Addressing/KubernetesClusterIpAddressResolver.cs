namespace Monq.Core.MicroserviceAddressing.Addressing;

/// <summary>
/// Resolves addresses of regular Kubernetes ClusterIP Services.
/// </summary>
public sealed class KubernetesClusterIpAddressResolver : IMicroserviceAddressResolver
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
        var port = context.PortOverride ?? context.Protocol switch
        {
            MicroserviceProtocol.Http => 80,
            MicroserviceProtocol.Grpc => 5006,
            _ => throw new ArgumentOutOfRangeException(nameof(context), context.Protocol, "Unsupported protocol."),
        };

        return new(new Uri(
            $"http://{context.TargetApplicationName}.{namespaceName}.svc.cluster.local:{port}"));
    }
}
