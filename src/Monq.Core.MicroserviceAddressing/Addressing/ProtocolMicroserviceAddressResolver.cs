namespace Monq.Core.MicroserviceAddressing.Addressing;

/// <summary>
/// Selects an address resolver according to the requested microservice protocol.
/// </summary>
public sealed class ProtocolMicroserviceAddressResolver : IMicroserviceAddressResolver
{
    readonly IMicroserviceAddressResolver _httpResolver;
    readonly IMicroserviceAddressResolver _grpcResolver;

    /// <summary>
    /// Initializes a protocol-aware address resolver.
    /// </summary>
    /// <param name="httpResolver">The resolver used for HTTP addresses.</param>
    /// <param name="grpcResolver">The resolver used for gRPC addresses.</param>
    public ProtocolMicroserviceAddressResolver(
        IMicroserviceAddressResolver httpResolver,
        IMicroserviceAddressResolver grpcResolver)
    {
        ArgumentNullException.ThrowIfNull(httpResolver);
        ArgumentNullException.ThrowIfNull(grpcResolver);

        _httpResolver = httpResolver;
        _grpcResolver = grpcResolver;
    }

    /// <inheritdoc />
    public ResolvedMicroserviceAddress Resolve(MicroserviceAddressContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var resolver = context.Protocol switch
        {
            MicroserviceProtocol.Http => _httpResolver,
            MicroserviceProtocol.Grpc => _grpcResolver,
            _ => throw new ArgumentOutOfRangeException(
                nameof(context),
                context.Protocol,
                "Unsupported protocol."),
        };

        return resolver.Resolve(context);
    }
}
