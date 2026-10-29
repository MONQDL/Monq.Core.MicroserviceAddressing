using Monq.Core.MicroserviceAddressing.Addressing;

namespace Monq.Core.MicroserviceAddressing.DependencyInjection;

internal sealed record MicroserviceAddressResolverRegistration(
    MicroserviceAddressResolverScope Scope,
    IMicroserviceAddressResolver Resolver);
