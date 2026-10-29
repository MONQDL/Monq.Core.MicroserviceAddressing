using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Monq.Core.MicroserviceAddressing.Addressing;

namespace Monq.Core.MicroserviceAddressing.Tests;

public class MicroserviceAddressResolverTests
{
    static readonly IConfiguration _configuration = new ConfigurationBuilder().Build();
    static readonly IHostEnvironment _environment = new TestHostEnvironment("Integration");

    [Theory]
    [InlineData(MicroserviceProtocol.Http, 80)]
    [InlineData(MicroserviceProtocol.Grpc, 5006)]
    public void ClusterIpResolver_UsesProtocolDefaultPort(
        MicroserviceProtocol protocol,
        int expectedPort)
    {
        var resolver = new KubernetesClusterIpAddressResolver();

        var result = resolver.Resolve(CreateContext("orders-service", protocol));

        Assert.Equal(
            "orders-service.integration.svc.cluster.local",
            result.Address.Host);
        Assert.Equal(expectedPort, result.Address.Port);
        Assert.Equal(EndpointResolutionMode.Direct, result.ResolutionMode);
    }

    [Theory]
    [InlineData(MicroserviceProtocol.Http, "http://orders-service-http.integration.svc.cluster.local:5005/")]
    [InlineData(MicroserviceProtocol.Grpc, "dns:///orders-service-grpc.integration.svc.cluster.local:5006")]
    public void ServiceDiscoveryResolver_UsesProtocolConvention(
        MicroserviceProtocol protocol,
        string expectedAddress)
    {
        var resolver = new KubernetesServiceDiscoveryAddressResolver();

        var result = resolver.Resolve(CreateContext("orders-service", protocol));

        Assert.Equal(expectedAddress, result.Address.AbsoluteUri);
        Assert.Equal(EndpointResolutionMode.ServiceDiscovery, result.ResolutionMode);
    }

    [Fact]
    public void Resolver_UsesPortOverride()
    {
        var resolver = new KubernetesClusterIpAddressResolver();

        var result = resolver.Resolve(CreateContext(
            "orders-service",
            MicroserviceProtocol.Http,
            8080));

        Assert.Equal(8080, result.Address.Port);
    }

    [Theory]
    [InlineData(MicroserviceProtocol.Http, "https://http.example.com/")]
    [InlineData(MicroserviceProtocol.Grpc, "https://grpc.example.com/")]
    public void ProtocolResolver_UsesResolverForRequestedProtocol(
        MicroserviceProtocol protocol,
        string expectedAddress)
    {
        var resolver = new ProtocolMicroserviceAddressResolver(
            new FixedMicroserviceAddressResolver(new Uri("https://http.example.com")),
            new FixedMicroserviceAddressResolver(new Uri("https://grpc.example.com")));

        var result = resolver.Resolve(CreateContext("orders-service", protocol));

        Assert.Equal(expectedAddress, result.Address.AbsoluteUri);
    }

    [Fact]
    public void ServiceDiscoveryResolver_UsesGrpcPortOverride()
    {
        var resolver = new KubernetesServiceDiscoveryAddressResolver();

        var result = resolver.Resolve(CreateContext("orders-service", MicroserviceProtocol.Grpc, 9000));

        Assert.Equal(
            "dns:///orders-service-grpc.integration.svc.cluster.local:9000",
            result.Address.AbsoluteUri);
    }

    [Fact]
    public void FixedResolver_IgnoresProtocolAndPortOverride()
    {
        var resolver = new FixedMicroserviceAddressResolver(new Uri("https://fixed.example"));

        var http = resolver.Resolve(CreateContext("orders-service", MicroserviceProtocol.Http));
        var grpc = resolver.Resolve(CreateContext("other-service", MicroserviceProtocol.Grpc, 9000));

        Assert.Equal("https://fixed.example/", http.Address.AbsoluteUri);
        Assert.Equal(http.Address, grpc.Address);
        Assert.Equal(EndpointResolutionMode.Direct, grpc.ResolutionMode);
    }

    [Fact]
    public void FixedResolver_ThrowsOnRelativeAddress() =>
        Assert.Throws<ArgumentException>(
            () => new FixedMicroserviceAddressResolver(new Uri("/relative/path", UriKind.Relative)));

    [Fact]
    public void FixedResolver_ThrowsOnNullAddress() =>
        Assert.Throws<ArgumentNullException>(() => new FixedMicroserviceAddressResolver(null!));

    [Fact]
    public void ProtocolResolver_ThrowsOnNullResolvers()
    {
        var inner = new KubernetesClusterIpAddressResolver();

        Assert.Throws<ArgumentNullException>(() => new ProtocolMicroserviceAddressResolver(null!, inner));
        Assert.Throws<ArgumentNullException>(() => new ProtocolMicroserviceAddressResolver(inner, null!));
    }

    [Fact]
    public void ProtocolResolver_ThrowsOnUnsupportedProtocol()
    {
        var resolver = new ProtocolMicroserviceAddressResolver(
            new KubernetesClusterIpAddressResolver(),
            new KubernetesClusterIpAddressResolver());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => resolver.Resolve(CreateContext("orders-service", (MicroserviceProtocol)99)));
    }

    [Fact]
    public void ClusterIpResolver_ThrowsOnUnsupportedProtocol()
    {
        var resolver = new KubernetesClusterIpAddressResolver();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => resolver.Resolve(CreateContext("orders-service", (MicroserviceProtocol)99)));
    }

    [Fact]
    public void ServiceDiscoveryResolver_ThrowsOnUnsupportedProtocol()
    {
        var resolver = new KubernetesServiceDiscoveryAddressResolver();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => resolver.Resolve(CreateContext("orders-service", (MicroserviceProtocol)99)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ClusterIpResolver_ThrowsOnEmptyApplicationName(string? applicationName)
    {
        var resolver = new KubernetesClusterIpAddressResolver();

        Assert.Throws<ArgumentException>(
            () => resolver.Resolve(CreateContext(applicationName!, MicroserviceProtocol.Http)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ServiceDiscoveryResolver_ThrowsOnEmptyApplicationName(string? applicationName)
    {
        var resolver = new KubernetesServiceDiscoveryAddressResolver();

        Assert.Throws<ArgumentException>(
            () => resolver.Resolve(CreateContext(applicationName!, MicroserviceProtocol.Grpc)));
    }

    public static TheoryData<IMicroserviceAddressResolver> AllResolvers => new()
    {
        new KubernetesClusterIpAddressResolver(),
        new KubernetesServiceDiscoveryAddressResolver(),
        new FixedMicroserviceAddressResolver(new Uri("https://fixed.example")),
        new ProtocolMicroserviceAddressResolver(
            new KubernetesClusterIpAddressResolver(),
            new KubernetesServiceDiscoveryAddressResolver()),
    };

    [Theory]
    [MemberData(nameof(AllResolvers))]
    public void Resolver_ThrowsOnNullContext(IMicroserviceAddressResolver resolver) =>
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));

    static MicroserviceAddressContext CreateContext(
        string applicationName,
        MicroserviceProtocol protocol,
        int? port = null) =>
        new(_configuration, _environment, applicationName, protocol, port);

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
