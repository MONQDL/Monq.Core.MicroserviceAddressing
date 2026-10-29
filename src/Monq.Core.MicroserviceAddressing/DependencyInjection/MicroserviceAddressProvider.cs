using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Monq.Core.MicroserviceAddressing.Addressing;

namespace Monq.Core.MicroserviceAddressing.DependencyInjection;

internal sealed class MicroserviceAddressProvider : IMicroserviceAddressProvider
{
    readonly IConfiguration _configuration;
    readonly IHostEnvironment _environment;
    readonly IReadOnlyList<MicroserviceAddressResolverRegistration> _registrations;
    readonly IMicroserviceAddressResolver _builtInResolver;

    public MicroserviceAddressProvider(
        IConfiguration configuration,
        IHostEnvironment environment,
        IEnumerable<MicroserviceAddressResolverRegistration> registrations,
        KubernetesClusterIpAddressResolver builtInResolver)
    {
        _configuration = configuration;
        _environment = environment;
        _registrations = registrations.ToArray();
        _builtInResolver = builtInResolver;
    }

    public ResolvedMicroserviceAddress Resolve(
        string applicationName,
        MicroserviceProtocol protocol,
        int? port = null,
        IMicroserviceAddressResolver? clientResolver = null)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
            throw new ArgumentException("The application name cannot be empty.", nameof(applicationName));

        var resolver = MicroserviceAddressResolverSelector.Select(
            protocol,
            clientResolver,
            ResolveRegistration,
            _builtInResolver);

        return resolver.Resolve(new(
            _configuration,
            _environment,
            applicationName,
            protocol,
            port));
    }

    IMicroserviceAddressResolver? ResolveRegistration(MicroserviceAddressResolverScope scope)
    {
        var registration = _registrations.LastOrDefault(x => x.Scope == scope);
        if (registration is null)
            return null;

        return registration.Resolver;
    }
}
