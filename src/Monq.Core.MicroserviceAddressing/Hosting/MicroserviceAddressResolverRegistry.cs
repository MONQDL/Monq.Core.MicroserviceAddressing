using Microsoft.Extensions.Hosting;
using Monq.Core.MicroserviceAddressing.Addressing;
using Monq.Core.MicroserviceAddressing.DependencyInjection;

namespace Monq.Core.MicroserviceAddressing.Hosting;

internal static class MicroserviceAddressResolverRegistry
{
    static readonly object _registryKey = new();
    static readonly object _sync = new();

    internal static IHostBuilder SetDefault(
        IHostBuilder hostBuilder,
        Func<HostBuilderContext, IMicroserviceAddressResolver> resolverFactory) =>
        Set(hostBuilder, state => state.Default = new(resolverFactory));

    internal static IHostBuilder SetHttp(
        IHostBuilder hostBuilder,
        Func<HostBuilderContext, IMicroserviceAddressResolver> resolverFactory) =>
        Set(hostBuilder, state => state.Http = new(resolverFactory));

    internal static IHostBuilder SetGrpc(
        IHostBuilder hostBuilder,
        Func<HostBuilderContext, IMicroserviceAddressResolver> resolverFactory) =>
        Set(hostBuilder, state => state.Grpc = new(resolverFactory));

    internal static IHostBuilder TrySetDefault(
        IHostBuilder hostBuilder,
        Func<HostBuilderContext, IMicroserviceAddressResolver> resolverFactory)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentNullException.ThrowIfNull(resolverFactory);

        lock (_sync)
        {
            GetOrCreateState(hostBuilder).Default ??= new(resolverFactory);
        }

        return hostBuilder;
    }

    internal static IMicroserviceAddressResolver? Get(
        IHostBuilder hostBuilder,
        HostBuilderContext context,
        MicroserviceAddressResolverScope scope)
    {
        ResolverRegistration? registration;
        lock (_sync)
        {
            var state = TryGetState(hostBuilder);
            registration = scope switch
            {
                MicroserviceAddressResolverScope.Default => state?.Default,
                MicroserviceAddressResolverScope.Http => state?.Http,
                MicroserviceAddressResolverScope.Grpc => state?.Grpc,
                _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported resolver scope."),
            };
        }

        return registration?.GetResolver(context);
    }

    static IHostBuilder Set(
        IHostBuilder hostBuilder,
        Action<ResolverRegistryState> configure)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentNullException.ThrowIfNull(configure);

        lock (_sync)
        {
            configure(GetOrCreateState(hostBuilder));
        }

        return hostBuilder;
    }

    // Callers must hold _sync: IHostBuilder.Properties is a plain dictionary.
    static ResolverRegistryState GetOrCreateState(IHostBuilder hostBuilder)
    {
        if (TryGetState(hostBuilder) is { } state)
            return state;

        state = new();
        hostBuilder.Properties[_registryKey] = state;
        return state;
    }

    static ResolverRegistryState? TryGetState(IHostBuilder hostBuilder) =>
        hostBuilder.Properties.TryGetValue(_registryKey, out var value)
            ? (ResolverRegistryState)value
            : null;

    sealed class ResolverRegistryState
    {
        internal ResolverRegistration? Default { get; set; }

        internal ResolverRegistration? Http { get; set; }

        internal ResolverRegistration? Grpc { get; set; }
    }

    sealed class ResolverRegistration
    {
        readonly object _sync = new();
        readonly Func<HostBuilderContext, IMicroserviceAddressResolver> _factory;
        IMicroserviceAddressResolver? _resolver;

        internal ResolverRegistration(
            Func<HostBuilderContext, IMicroserviceAddressResolver> factory)
        {
            _factory = factory;
        }

        internal IMicroserviceAddressResolver GetResolver(HostBuilderContext context)
        {
            var resolver = Volatile.Read(ref _resolver);
            if (resolver is not null)
                return resolver;

            lock (_sync)
            {
                resolver = Volatile.Read(ref _resolver);
                if (resolver is null)
                {
                    resolver = _factory(context)
                        ?? throw new InvalidOperationException(
                            "The microservice address resolver factory returned null.");
                    Volatile.Write(ref _resolver, resolver);
                }

                return resolver;
            }
        }
    }
}
