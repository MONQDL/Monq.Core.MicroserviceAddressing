using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Monq.Core.MicroserviceAddressing.Addressing;
using Monq.Core.MicroserviceAddressing.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering microservice address resolution, selecting
/// address resolvers, and inspecting address resolution on a service collection.
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

    /// <summary>
    /// Evaluates the resolver selection registered on <paramref name="services"/> and returns
    /// the endpoint resolution mode the address provider would produce for a client.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Intended for client registrations that must decide discovery-specific behavior at
    /// registration time, before a service provider is available. The configuration and the
    /// environment are taken from the registered descriptors, and a registration factory is
    /// invoked with an empty service provider. Returns <see langword="null"/> when either
    /// service is unavailable, when the protocol is unsupported, or when the resolver throws.
    /// </para>
    /// <para>
    /// Peeking invokes the selected resolver in addition to the resolution performed by
    /// <see cref="IMicroserviceAddressProvider"/>. Resolvers must therefore be side-effect
    /// free and must not resolve application services to compute the resolution mode.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to inspect.</param>
    /// <param name="applicationName">Kubernetes application name of the target microservice.</param>
    /// <param name="protocol">Target protocol.</param>
    /// <param name="port">Optional service port override.</param>
    /// <param name="clientResolver">
    /// Resolver registered for a single client. It takes precedence over the registered
    /// protocol and default resolvers, mirroring
    /// <see cref="IMicroserviceAddressProvider.Resolve(string, MicroserviceProtocol, int?, IMicroserviceAddressResolver?)"/>.
    /// </param>
    /// <returns>
    /// The resolution mode of the address the provider would return, or <see langword="null"/>
    /// when the mode cannot be determined at registration time.
    /// </returns>
    public static EndpointResolutionMode? TryPeekEndpointResolutionMode(
        this IServiceCollection services,
        string applicationName,
        MicroserviceProtocol protocol,
        int? port = null,
        IMicroserviceAddressResolver? clientResolver = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(applicationName))
            throw new ArgumentException("The application name cannot be empty.", nameof(applicationName));

        var configuration = PeekService<IConfiguration>(services);
        var environment = PeekService<IHostEnvironment>(services);
        if (configuration is null || environment is null)
            return null;

        IMicroserviceAddressResolver? ResolveRegistration(MicroserviceAddressResolverScope scope) =>
            (services.LastOrDefault(descriptor =>
                    descriptor.ServiceType == typeof(MicroserviceAddressResolverRegistration)
                    && descriptor.ImplementationInstance is MicroserviceAddressResolverRegistration registration
                    && registration.Scope == scope)
                ?.ImplementationInstance as MicroserviceAddressResolverRegistration)?.Resolver;

        try
        {
            var resolver = MicroserviceAddressResolverSelector.Select(
                protocol,
                clientResolver,
                ResolveRegistration,
                GetBuiltInResolver(services));

            return resolver.Resolve(new(configuration, environment, applicationName, protocol, port))
                .ResolutionMode;
        }
        catch (Exception)
        {
            return null;
        }
    }

    static IMicroserviceAddressResolver GetBuiltInResolver(IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(
            x => x.ServiceType == typeof(KubernetesClusterIpAddressResolver));

        return descriptor?.ImplementationInstance as KubernetesClusterIpAddressResolver
            ?? new KubernetesClusterIpAddressResolver();
    }

    static T? PeekService<T>(IServiceCollection services)
        where T : class
    {
        var descriptor = services.LastOrDefault(x => x.ServiceType == typeof(T));

        if (descriptor?.ImplementationInstance is T instance)
            return instance;

        if (descriptor?.ImplementationFactory is { } factory)
        {
            try
            {
                return factory(EmptyServiceProvider.Instance) as T;
            }
            catch (Exception)
            {
                return null;
            }
        }

        return null;
    }

    sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();

        public object? GetService(Type serviceType) => null;
    }
}
