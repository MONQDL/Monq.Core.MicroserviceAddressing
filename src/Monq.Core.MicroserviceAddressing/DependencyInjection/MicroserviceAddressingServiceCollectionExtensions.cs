using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monq.Core.MicroserviceAddressing.Addressing;

namespace Monq.Core.MicroserviceAddressing.DependencyInjection;

/// <summary>
/// Extension methods for registering microservice address resolution.
/// </summary>
public static class MicroserviceAddressingServiceCollectionExtensions
{
    /// <summary>
    /// Adds the address provider and the built-in Kubernetes ClusterIP fallback resolver.
    /// </summary>
    public static IServiceCollection AddMicroserviceAddressing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<KubernetesClusterIpAddressResolver>();
        services.TryAddSingleton<IMicroserviceAddressProvider, MicroserviceAddressProvider>();
        return services;
    }

    internal static IServiceCollection AddResolverRegistration(
        this IServiceCollection services,
        MicroserviceAddressResolverScope scope,
        IMicroserviceAddressResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resolver);

        services.AddMicroserviceAddressing();
        services.AddSingleton(new MicroserviceAddressResolverRegistration(scope, resolver));
        return services;
    }
}
