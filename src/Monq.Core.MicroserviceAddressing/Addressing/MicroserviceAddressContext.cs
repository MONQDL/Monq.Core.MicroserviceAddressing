using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Monq.Core.MicroserviceAddressing.Addressing;

/// <summary>
/// Describes a microservice address resolution request.
/// </summary>
/// <param name="Configuration">Application configuration.</param>
/// <param name="Environment">Host environment of the current application.</param>
/// <param name="TargetApplicationName">
/// Kubernetes application name of the microservice being resolved. Unlike
/// <see cref="IHostEnvironment.ApplicationName"/> this is the target service,
/// not the current application.
/// </param>
/// <param name="Protocol">Target protocol.</param>
/// <param name="PortOverride">Optional service port override.</param>
public sealed record MicroserviceAddressContext(
    IConfiguration Configuration,
    IHostEnvironment Environment,
    string TargetApplicationName,
    MicroserviceProtocol Protocol,
    int? PortOverride = null);
