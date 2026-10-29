using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monq.Core.MicroserviceAddressing.Addressing;
using Monq.Core.MicroserviceAddressing.DependencyInjection;

namespace Monq.Core.MicroserviceAddressing.Hosting;

/// <summary>
/// Extension methods for selecting and invoking microservice address resolvers on a host builder.
/// </summary>
public static class MicroserviceAddressingHostBuilderExtensions
{
    static readonly IMicroserviceAddressResolver _builtInResolver =
        new KubernetesClusterIpAddressResolver();

    /// <summary>Sets the default resolver for the host and the address provider registered in DI.</summary>
    /// <remarks>
    /// The last call for a scope wins. To override a default previously selected by a
    /// library, call this method after the library configuration.
    /// </remarks>
    public static IHostBuilder UseMicroserviceAddressResolver(
        this IHostBuilder hostBuilder,
        IMicroserviceAddressResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        return hostBuilder.UseMicroserviceAddressResolver(_ => resolver);
    }

    /// <summary>Sets the default resolver using the final host context.</summary>
    /// <remarks>
    /// The last call for a scope wins. The factory runs when the host executes its
    /// <c>ConfigureServices</c> delegates, so it observes the final host configuration.
    /// </remarks>
    public static IHostBuilder UseMicroserviceAddressResolver(
        this IHostBuilder hostBuilder,
        Func<HostBuilderContext, IMicroserviceAddressResolver> resolverFactory)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentNullException.ThrowIfNull(resolverFactory);

        MicroserviceAddressResolverRegistry.SetDefault(hostBuilder, resolverFactory);
        return hostBuilder.ConfigureServices((context, services) =>
            services.AddResolverRegistration(
                MicroserviceAddressResolverScope.Default,
                GetRequiredResolver(
                    hostBuilder,
                    context,
                    MicroserviceAddressResolverScope.Default)));
    }

    /// <summary>
    /// Sets the default resolver only when no default resolver has been selected yet.
    /// </summary>
    /// <remarks>
    /// Intended for libraries that install a fallback policy. A resolver selected before
    /// this call is preserved, and a later <see cref="UseMicroserviceAddressResolver(IHostBuilder, IMicroserviceAddressResolver)"/>
    /// call still overrides the selection made here.
    /// </remarks>
    public static IHostBuilder TryUseMicroserviceAddressResolver(
        this IHostBuilder hostBuilder,
        IMicroserviceAddressResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        return hostBuilder.TryUseMicroserviceAddressResolver(_ => resolver);
    }

    /// <summary>
    /// Sets the default resolver using the final host context, but only when no default
    /// resolver has been selected yet.
    /// </summary>
    /// <remarks>
    /// Intended for libraries that install a fallback policy. A resolver selected before
    /// this call is preserved, and a later <see cref="UseMicroserviceAddressResolver(IHostBuilder, Func{HostBuilderContext, IMicroserviceAddressResolver})"/>
    /// call still overrides the selection made here. When a resolver already exists, this
    /// factory is never invoked.
    /// </remarks>
    public static IHostBuilder TryUseMicroserviceAddressResolver(
        this IHostBuilder hostBuilder,
        Func<HostBuilderContext, IMicroserviceAddressResolver> resolverFactory)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentNullException.ThrowIfNull(resolverFactory);

        MicroserviceAddressResolverRegistry.TrySetDefault(hostBuilder, resolverFactory);
        return hostBuilder.ConfigureServices((context, services) =>
            services.AddResolverRegistration(
                MicroserviceAddressResolverScope.Default,
                GetRequiredResolver(
                    hostBuilder,
                    context,
                    MicroserviceAddressResolverScope.Default)));
    }

    /// <summary>Sets the HTTP resolver for the host and the address provider registered in DI.</summary>
    /// <remarks>
    /// Overrides the default resolver for HTTP. The last call for a scope wins and
    /// takes precedence over the default resolver.
    /// </remarks>
    public static IHostBuilder UseHttpMicroserviceAddressResolver(
        this IHostBuilder hostBuilder,
        IMicroserviceAddressResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        return hostBuilder.UseHttpMicroserviceAddressResolver(_ => resolver);
    }

    /// <summary>Sets the HTTP resolver using the final host context.</summary>
    /// <remarks>
    /// The last call for a scope wins and takes precedence over the default resolver.
    /// </remarks>
    public static IHostBuilder UseHttpMicroserviceAddressResolver(
        this IHostBuilder hostBuilder,
        Func<HostBuilderContext, IMicroserviceAddressResolver> resolverFactory)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentNullException.ThrowIfNull(resolverFactory);

        MicroserviceAddressResolverRegistry.SetHttp(hostBuilder, resolverFactory);
        return hostBuilder.ConfigureServices((context, services) =>
            services.AddResolverRegistration(
                MicroserviceAddressResolverScope.Http,
                GetRequiredResolver(
                    hostBuilder,
                    context,
                    MicroserviceAddressResolverScope.Http)));
    }

    /// <summary>Sets the gRPC resolver for the host and the address provider registered in DI.</summary>
    /// <remarks>
    /// Overrides the default resolver for gRPC. The last call for a scope wins and
    /// takes precedence over the default resolver.
    /// </remarks>
    public static IHostBuilder UseGrpcMicroserviceAddressResolver(
        this IHostBuilder hostBuilder,
        IMicroserviceAddressResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        return hostBuilder.UseGrpcMicroserviceAddressResolver(_ => resolver);
    }

    /// <summary>Sets the gRPC resolver using the final host context.</summary>
    /// <remarks>
    /// The last call for a scope wins and takes precedence over the default resolver.
    /// </remarks>
    public static IHostBuilder UseGrpcMicroserviceAddressResolver(
        this IHostBuilder hostBuilder,
        Func<HostBuilderContext, IMicroserviceAddressResolver> resolverFactory)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentNullException.ThrowIfNull(resolverFactory);

        MicroserviceAddressResolverRegistry.SetGrpc(hostBuilder, resolverFactory);
        return hostBuilder.ConfigureServices((context, services) =>
            services.AddResolverRegistration(
                MicroserviceAddressResolverScope.Grpc,
                GetRequiredResolver(
                    hostBuilder,
                    context,
                    MicroserviceAddressResolverScope.Grpc)));
    }

    /// <summary>
    /// Resolves a microservice address using the client resolver, protocol resolver,
    /// default resolver, or built-in ClusterIP resolver, in that order.
    /// </summary>
    public static ResolvedMicroserviceAddress ResolveMicroserviceAddress(
        this IHostBuilder hostBuilder,
        HostBuilderContext context,
        string applicationName,
        MicroserviceProtocol protocol,
        int? port = null,
        IMicroserviceAddressResolver? addressResolver = null)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(applicationName))
            throw new ArgumentException("The application name cannot be empty.", nameof(applicationName));

        var resolver = MicroserviceAddressResolverSelector.Select(
            protocol,
            addressResolver,
            scope => MicroserviceAddressResolverRegistry.Get(hostBuilder, context, scope),
            _builtInResolver);

        return resolver.Resolve(new(
            context.Configuration,
            context.HostingEnvironment,
            applicationName,
            protocol,
            port));
    }

    // The DI registration is resolved through the host-level registry while ConfigureServices
    // runs instead of capturing the factory at Use* call time. A later Use* call replaces the
    // registry entry, so the runtime IMicroserviceAddressProvider always observes the same
    // final selection as ResolveMicroserviceAddress during registration.
    static IMicroserviceAddressResolver GetRequiredResolver(
        IHostBuilder hostBuilder,
        HostBuilderContext context,
        MicroserviceAddressResolverScope scope) =>
        MicroserviceAddressResolverRegistry.Get(hostBuilder, context, scope)
            ?? throw new InvalidOperationException(
                $"No microservice address resolver is configured for the '{scope}' scope.");
}
