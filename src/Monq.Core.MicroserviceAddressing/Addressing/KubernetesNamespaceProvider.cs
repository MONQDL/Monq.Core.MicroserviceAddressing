namespace Monq.Core.MicroserviceAddressing.Addressing;

/// <summary>
/// Provides the Kubernetes namespace name for the built-in Kubernetes address resolvers.
/// </summary>
public static class KubernetesNamespaceProvider
{
    /// <summary>
    /// Gets the Kubernetes namespace name for the specified addressing context.
    /// The <c>ASPNETCORE_URL_NAMESPACE</c> environment value is used as provided when set,
    /// otherwise the environment name is normalized to lowercase.
    /// </summary>
    /// <param name="context">The addressing context.</param>
    /// <returns>The Kubernetes namespace name.</returns>
    public static string GetNamespaceName(MicroserviceAddressContext context)
    {
        var configuredNamespace = Environment
            .GetEnvironmentVariable(AddressingConstants.KubernetesNamespaceEnvironmentVariable);

        return string.IsNullOrWhiteSpace(configuredNamespace)
            ? context.Environment.EnvironmentName.ToLowerInvariant()
            : configuredNamespace.Trim();
    }
}
