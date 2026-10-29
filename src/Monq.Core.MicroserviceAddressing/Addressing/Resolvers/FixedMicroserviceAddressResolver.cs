namespace Monq.Core.MicroserviceAddressing.Addressing;

/// <summary>
/// Resolves every microservice to the same fixed address and endpoint resolution mode.
/// Protocol, target application name, and <see cref="MicroserviceAddressContext.PortOverride"/>
/// of the resolution context are ignored: the supplied absolute address is returned as is.
/// </summary>
public sealed class FixedMicroserviceAddressResolver : IMicroserviceAddressResolver
{
    readonly ResolvedMicroserviceAddress _address;

    /// <summary>Initializes the resolver with a fixed address.</summary>
    public FixedMicroserviceAddressResolver(
        Uri address,
        EndpointResolutionMode resolutionMode = EndpointResolutionMode.Direct)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (!address.IsAbsoluteUri)
            throw new ArgumentException("The microservice address must be absolute.", nameof(address));

        _address = new(address, resolutionMode);
    }

    /// <inheritdoc />
    public ResolvedMicroserviceAddress Resolve(MicroserviceAddressContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _address;
    }
}
