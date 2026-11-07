using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Monq.Core.MicroserviceAddressing.Addressing;

namespace Monq.Core.MicroserviceAddressing.Tests;

public class MicroserviceAddressPeekTests
{
    [Fact]
    public void Peek_BuiltInResolver_ReturnsDirect()
    {
        var services = CreateServices();

        var mode = services.TryPeekEndpointResolutionMode("orders-service", MicroserviceProtocol.Http);

        Assert.Equal(EndpointResolutionMode.Direct, mode);
    }

    [Fact]
    public void Peek_ProtocolResolver_ReturnsItsMode()
    {
        var services = CreateServices();
        services.UseHttpMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("http://orders-service-http.integration.svc.cluster.local:5005"),
            EndpointResolutionMode.ServiceDiscovery));

        var http = services.TryPeekEndpointResolutionMode("orders-service", MicroserviceProtocol.Http);
        var grpc = services.TryPeekEndpointResolutionMode("orders-service", MicroserviceProtocol.Grpc);

        Assert.Equal(EndpointResolutionMode.ServiceDiscovery, http);
        Assert.Equal(EndpointResolutionMode.Direct, grpc);
    }

    [Fact]
    public void Peek_DefaultResolver_ReturnsItsMode()
    {
        var services = CreateServices();
        services.UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("dns:///orders-service-grpc.integration.svc.cluster.local:5006"),
            EndpointResolutionMode.ServiceDiscovery));

        var http = services.TryPeekEndpointResolutionMode("orders-service", MicroserviceProtocol.Http);
        var grpc = services.TryPeekEndpointResolutionMode("orders-service", MicroserviceProtocol.Grpc);

        Assert.Equal(EndpointResolutionMode.ServiceDiscovery, http);
        Assert.Equal(EndpointResolutionMode.ServiceDiscovery, grpc);
    }

    [Fact]
    public void Peek_ClientResolverOverridesRegisteredResolvers()
    {
        var services = CreateServices();
        services.UseHttpMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("http://orders-service.integration.svc.cluster.local")));

        var mode = services.TryPeekEndpointResolutionMode(
            "orders-service",
            MicroserviceProtocol.Http,
            clientResolver: new FixedMicroserviceAddressResolver(
                new Uri("http://orders-service-http.integration.svc.cluster.local:5005"),
                EndpointResolutionMode.ServiceDiscovery));

        Assert.Equal(EndpointResolutionMode.ServiceDiscovery, mode);
    }

    [Fact]
    public void Peek_LastRegistrationWins()
    {
        var services = CreateServices();
        services.UseHttpMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("http://first.example"),
            EndpointResolutionMode.ServiceDiscovery));
        services.UseHttpMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("http://second.example")));

        var mode = services.TryPeekEndpointResolutionMode("orders-service", MicroserviceProtocol.Http);

        Assert.Equal(EndpointResolutionMode.Direct, mode);
    }

    [Fact]
    public void Peek_FactoryConfiguration_IsInvoked()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            _ => new ConfigurationBuilder().Build());
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("Integration"));
        services.AddMicroserviceAddressing();

        var mode = services.TryPeekEndpointResolutionMode("orders-service", MicroserviceProtocol.Http);

        Assert.Equal(EndpointResolutionMode.Direct, mode);
    }

    [Fact]
    public void Peek_MissingEnvironment_ReturnsNull()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddMicroserviceAddressing();

        Assert.Null(services.TryPeekEndpointResolutionMode("orders-service", MicroserviceProtocol.Http));
    }

    [Fact]
    public void Peek_MissingConfiguration_ReturnsNull()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("Integration"));
        services.AddMicroserviceAddressing();

        Assert.Null(services.TryPeekEndpointResolutionMode("orders-service", MicroserviceProtocol.Http));
    }

    [Fact]
    public void Peek_ThrowingResolver_ReturnsNull()
    {
        var services = CreateServices();
        services.UseMicroserviceAddressResolver(new ThrowingAddressResolver());

        Assert.Null(services.TryPeekEndpointResolutionMode("orders-service", MicroserviceProtocol.Http));
    }

    [Fact]
    public void Peek_UnsupportedProtocol_ReturnsNull()
    {
        var services = CreateServices();

        Assert.Null(services.TryPeekEndpointResolutionMode("orders-service", (MicroserviceProtocol)99));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Peek_ThrowsOnEmptyApplicationName(string? applicationName)
    {
        var services = CreateServices();

        Assert.Throws<ArgumentException>(
            () => services.TryPeekEndpointResolutionMode(applicationName!, MicroserviceProtocol.Http));
    }

    [Fact]
    public void Peek_UsesPortOverride()
    {
        var services = CreateServices();
        services.UseHttpMicroserviceAddressResolver(new RecordingAddressResolver());

        services.TryPeekEndpointResolutionMode("orders-service", MicroserviceProtocol.Http, 9000);

        Assert.Equal(9000, RecordingAddressResolver.LastContext!.PortOverride);
    }

    static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("Integration"));
        services.AddMicroserviceAddressing();
        return services;
    }

    sealed class ThrowingAddressResolver : IMicroserviceAddressResolver
    {
        public ResolvedMicroserviceAddress Resolve(MicroserviceAddressContext context) =>
            throw new InvalidOperationException("Resolver failed.");
    }

    sealed class RecordingAddressResolver : IMicroserviceAddressResolver
    {
        public static MicroserviceAddressContext? LastContext { get; set; }

        public ResolvedMicroserviceAddress Resolve(MicroserviceAddressContext context)
        {
            LastContext = context;
            return new(new Uri("http://orders-service.integration.svc.cluster.local"));
        }
    }

    sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName) =>
            EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
