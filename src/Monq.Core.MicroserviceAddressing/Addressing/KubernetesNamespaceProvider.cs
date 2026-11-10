namespace Monq.Core.MicroserviceAddressing.Addressing;

/// <summary>
/// Provides the Kubernetes namespace name for the built-in Kubernetes address resolvers.
/// </summary>
internal static class KubernetesNamespaceProvider
{
    internal static string GetNamespaceName(MicroserviceAddressContext context)
    {
        var configuredNamespace = Environment
            .GetEnvironmentVariable(AddressingConstants.KubernetesNamespaceEnvironmentVariable);

        return string.IsNullOrWhiteSpace(configuredNamespace)
            ? context.Environment.EnvironmentName.ToLowerInvariant()
            : configuredNamespace.Trim();
    }
}
