namespace Monq.Core.MicroserviceAddressing.Addressing;

/// <summary>
/// Defines the supported microservice protocols.
/// </summary>
public enum MicroserviceProtocol
{
    /// <summary>HTTP.</summary>
    Http,

    /// <summary>gRPC over HTTP/2.</summary>
    Grpc,
}
