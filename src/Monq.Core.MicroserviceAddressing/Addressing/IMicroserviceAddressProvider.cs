namespace Monq.Core.MicroserviceAddressing.Addressing;

/// <summary>
/// Selects an address resolver and resolves microservice addresses for the current application.
/// </summary>
public interface IMicroserviceAddressProvider
{
    /// <summary>
    /// Resolves a microservice address using the client resolver, protocol resolver,
    /// default resolver, or built-in resolver, in that order.
    /// </summary>
    /// <param name="applicationName">Kubernetes application name of the target microservice.</param>
    /// <param name="protocol">Target protocol.</param>
    /// <param name="port">
    /// Optional service port override. Protocol-agnostic resolvers such as
    /// <see cref="FixedMicroserviceAddressResolver"/> ignore it.
    /// </param>
    /// <param name="clientResolver">Optional resolver for a single client registration.</param>
    /// <remarks>
    /// Client libraries are expected to honor
    /// <see cref="ResolvedMicroserviceAddress.ResolutionMode"/>. gRPC client libraries
    /// additionally recognize a client-side service discovery address by the <c>dns</c>
    /// URI scheme and enable DNS resolution and load balancing for it.
    /// </remarks>
    ResolvedMicroserviceAddress Resolve(
        string applicationName,
        MicroserviceProtocol protocol,
        int? port = null,
        IMicroserviceAddressResolver? clientResolver = null);
}
