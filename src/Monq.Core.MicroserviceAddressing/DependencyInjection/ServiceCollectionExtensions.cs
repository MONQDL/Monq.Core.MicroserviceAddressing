using Microsoft.Extensions.DependencyInjection.Extensions;
using Monq.Core.MicroserviceAddressing.Addressing;
using Monq.Core.MicroserviceAddressing.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering microservice address resolution and selecting
/// address resolvers on a service collection.
/// </summary>
/// <remarks>
/// Resolver registrations are observed by <see cref="IMicroserviceAddressProvider"/> when a
/// client resolves an address, so the last registration for a scope wins regardless of where
/// the resolver was selected. This makes the registrations safe to combine with
/// <c>ConfigureServices</c> callbacks on a host builder.
/// </remarks>
public static class ServiceCollectionExtensions
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

    /// <summary>Sets the default resolver for the application.</summary>
    /// <remarks>
    /// The last registration for the default scope wins. To override a default previously
    /// selected by a library, call this method after the library configuration.
    /// </remarks>
    public static IServiceCollection UseMicroserviceAddressResolver(
        this IServiceCollection services,
        IMicroserviceAddressResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resolver);

        return services.AddResolverRegistration(MicroserviceAddressResolverScope.Default, resolver);
    }

    /// <summary>
    /// Sets the default resolver only when no default resolver has been registered yet.
    /// </summary>
    /// <remarks>
    /// Intended for libraries that install a fallback policy. A resolver registered before
    /// this call is preserved, and a later
    /// <see cref="UseMicroserviceAddressResolver(IServiceCollection, IMicroserviceAddressResolver)"/>
    /// call still overrides the selection made here.
    /// </remarks>
    public static IServiceCollection TryUseMicroserviceAddressResolver(
        this IServiceCollection services,
        IMicroserviceAddressResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resolver);

        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(MicroserviceAddressResolverRegistration)
                && descriptor.ImplementationInstance is MicroserviceAddressResolverRegistration { Scope: MicroserviceAddressResolverScope.Default }))
            return services;

        return services.UseMicroserviceAddressResolver(resolver);
    }

    /// <summary>Sets the HTTP resolver for the application.</summary>
    /// <remarks>
    /// Overrides the default resolver for HTTP. The last registration for the HTTP scope
    /// wins and takes precedence over the default resolver.
    /// </remarks>
    public static IServiceCollection UseHttpMicroserviceAddressResolver(
        this IServiceCollection services,
        IMicroserviceAddressResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resolver);

        return services.AddResolverRegistration(MicroserviceAddressResolverScope.Http, resolver);
    }

    /// <summary>Sets the gRPC resolver for the application.</summary>
    /// <remarks>
    /// Overrides the default resolver for gRPC. The last registration for the gRPC scope
    /// wins and takes precedence over the default resolver.
    /// </remarks>
    public static IServiceCollection UseGrpcMicroserviceAddressResolver(
        this IServiceCollection services,
        IMicroserviceAddressResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resolver);

        return services.AddResolverRegistration(MicroserviceAddressResolverScope.Grpc, resolver);
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
