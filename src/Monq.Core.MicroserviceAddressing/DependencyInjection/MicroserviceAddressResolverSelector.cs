using Monq.Core.MicroserviceAddressing.Addressing;

namespace Monq.Core.MicroserviceAddressing.DependencyInjection;

internal static class MicroserviceAddressResolverSelector
{
    internal static IMicroserviceAddressResolver Select(
        MicroserviceProtocol protocol,
        IMicroserviceAddressResolver? clientResolver,
        Func<MicroserviceAddressResolverScope, IMicroserviceAddressResolver?> resolveRegistration,
        IMicroserviceAddressResolver fallbackResolver)
    {
        ArgumentNullException.ThrowIfNull(resolveRegistration);
        ArgumentNullException.ThrowIfNull(fallbackResolver);

        var protocolScope = protocol switch
        {
            MicroserviceProtocol.Http => MicroserviceAddressResolverScope.Http,
            MicroserviceProtocol.Grpc => MicroserviceAddressResolverScope.Grpc,
            _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Unsupported protocol."),
        };

        return clientResolver
            ?? resolveRegistration(protocolScope)
            ?? resolveRegistration(MicroserviceAddressResolverScope.Default)
            ?? fallbackResolver;
    }
}
