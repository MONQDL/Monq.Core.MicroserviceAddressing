namespace Monq.Core.MicroserviceAddressing.Addressing;

/// <summary>
/// Provides the Kubernetes namespace name for the built-in Kubernetes address resolvers.
/// </summary>
internal static class KubernetesNamespaceProvider
{
    /// <summary>
    /// Environment variable that explicitly specifies the Kubernetes namespace. When set to a non-empty
    /// value, it takes precedence over <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.EnvironmentName"/>.
    /// </summary>
    internal const string NamespaceEnvironmentVariable = "ASPNETCORE_URL_NAMESPACE";

    internal static string GetNamespaceName(MicroserviceAddressContext context)
    {
        var configuredNamespace = Environment.GetEnvironmentVariable(NamespaceEnvironmentVariable);

        return string.IsNullOrWhiteSpace(configuredNamespace)
            ? context.Environment.EnvironmentName.ToLowerInvariant()
            : configuredNamespace.Trim().ToLowerInvariant();
    }
}
