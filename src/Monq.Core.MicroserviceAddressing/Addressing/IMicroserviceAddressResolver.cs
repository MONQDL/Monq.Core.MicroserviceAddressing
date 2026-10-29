namespace Monq.Core.MicroserviceAddressing.Addressing;

/// <summary>
/// Resolves a microservice address according to a selected addressing policy.
/// Implementations must be thread-safe because a resolver can be used concurrently
/// by client registration and the runtime <see cref="IMicroserviceAddressProvider"/>.
/// </summary>
public interface IMicroserviceAddressResolver
{
    /// <summary>Resolves an address for the supplied context.</summary>
    ResolvedMicroserviceAddress Resolve(MicroserviceAddressContext context);
}
