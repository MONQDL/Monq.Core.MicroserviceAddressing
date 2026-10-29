namespace Monq.Core.MicroserviceAddressing.Addressing;

/// <summary>
/// Defines how a resolved endpoint must be used by a client. HTTP client libraries
/// select their endpoint handling from this mode. gRPC client libraries additionally
/// recognize a service discovery address by the <c>dns</c> URI scheme.
/// </summary>
public enum EndpointResolutionMode
{
    /// <summary>Connect directly to the resolved address.</summary>
    Direct,

    /// <summary>
    /// Resolve and balance individual service endpoints on the client.
    /// The resolved address must point to a Kubernetes headless Service that
    /// publishes pod endpoints.
    /// </summary>
    ServiceDiscovery,
}
