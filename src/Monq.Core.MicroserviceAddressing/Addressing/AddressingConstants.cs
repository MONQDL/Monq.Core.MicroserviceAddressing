namespace Monq.Core.MicroserviceAddressing.Addressing;

/// <summary>
/// Constants shared across the microservice addressing API.
/// </summary>
public static class AddressingConstants
{
    /// <summary>
    /// Environment variable that explicitly specifies the Kubernetes namespace used by the built-in
    /// Kubernetes address resolvers. When set to a non-empty value, the value is used as provided
    /// instead of <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.EnvironmentName"/>.
    /// </summary>
    public const string KubernetesNamespaceEnvironmentVariable = "ASPNETCORE_URL_NAMESPACE";
}
