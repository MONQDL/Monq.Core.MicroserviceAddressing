using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Monq.Core.MicroserviceAddressing.Addressing;
using Monq.Core.MicroserviceAddressing.DependencyInjection;

namespace Monq.Core.MicroserviceAddressing.Tests;

public class MicroserviceAddressProviderTests
{
    [Fact]
    public void Provider_UsesBuiltInClusterIpResolverByDefault()
    {
        using var serviceProvider = CreateServices().BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IMicroserviceAddressProvider>();

        var result = provider.Resolve("orders-service", MicroserviceProtocol.Http);

        Assert.Equal(
            "http://orders-service.integration.svc.cluster.local/",
            result.Address.AbsoluteUri);
    }

    [Fact]
    public void ProtocolResolver_OverridesDefaultResolver()
    {
        var services = CreateServices();
        services.UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://default.example")));
        services.UseGrpcMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://grpc.example")));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IMicroserviceAddressProvider>();

        var http = provider.Resolve("orders-service", MicroserviceProtocol.Http);
        var grpc = provider.Resolve("orders-service", MicroserviceProtocol.Grpc);

        Assert.Equal("https://default.example/", http.Address.AbsoluteUri);
        Assert.Equal("https://grpc.example/", grpc.Address.AbsoluteUri);
    }

    [Fact]
    public void ClientResolver_OverridesProtocolAndDefaultResolvers()
    {
        var services = CreateServices();
        services.UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://default.example")));
        services.UseHttpMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://http.example")));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IMicroserviceAddressProvider>();

        var result = provider.Resolve(
            "orders-service",
            MicroserviceProtocol.Http,
            clientResolver: new FixedMicroserviceAddressResolver(
                new Uri("https://client.example")));

        Assert.Equal("https://client.example/", result.Address.AbsoluteUri);
    }

    [Fact]
    public void LastProtocolRegistrationWins()
    {
        var services = CreateServices();
        services.UseHttpMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://first.example")));
        services.UseHttpMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://second.example")));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IMicroserviceAddressProvider>();

        var result = provider.Resolve("orders-service", MicroserviceProtocol.Http);

        Assert.Equal("https://second.example/", result.Address.AbsoluteUri);
    }

    [Fact]
    public void LastGrpcRegistrationWins()
    {
        var services = CreateServices();
        services.UseGrpcMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://first.example")));
        services.UseGrpcMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://second.example")));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IMicroserviceAddressProvider>();

        var result = provider.Resolve("orders-service", MicroserviceProtocol.Grpc);

        Assert.Equal("https://second.example/", result.Address.AbsoluteUri);
    }

    [Fact]
    public void LastDefaultRegistrationWins()
    {
        var services = CreateServices();
        services.UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://first.example")));
        services.UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://second.example")));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IMicroserviceAddressProvider>();

        var http = provider.Resolve("orders-service", MicroserviceProtocol.Http);
        var grpc = provider.Resolve("orders-service", MicroserviceProtocol.Grpc);

        Assert.Equal("https://second.example/", http.Address.AbsoluteUri);
        Assert.Equal("https://second.example/", grpc.Address.AbsoluteUri);
    }

    [Fact]
    public void Use_NullResolver_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => CreateServices().UseMicroserviceAddressResolver(null!));

        Assert.Throws<ArgumentNullException>(
            () => CreateServices().UseHttpMicroserviceAddressResolver(null!));
    }

    [Fact]
    public void Provider_UsesPortOverrideWithBuiltInResolver()
    {
        using var serviceProvider = CreateServices().BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IMicroserviceAddressProvider>();

        var result = provider.Resolve("orders-service", MicroserviceProtocol.Grpc, 9000);

        Assert.Equal(9000, result.Address.Port);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Provider_ThrowsOnEmptyApplicationName(string? applicationName)
    {
        using var serviceProvider = CreateServices().BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IMicroserviceAddressProvider>();

        Assert.Throws<ArgumentException>(
            () => provider.Resolve(applicationName!, MicroserviceProtocol.Http));
    }

    [Fact]
    public void Provider_ThrowsOnUnsupportedProtocol()
    {
        using var serviceProvider = CreateServices().BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IMicroserviceAddressProvider>();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => provider.Resolve("orders-service", (MicroserviceProtocol)99));
    }

    [Fact]
    public void HostConfigureServices_RegistrationsAreObservedByProvider()
    {
        using var host = new HostBuilder()
            .ConfigureServices((_, services) =>
            {
                services.UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                    new Uri("https://default.example")));
                services.UseGrpcMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                    new Uri("https://grpc.example")));
            })
            .Build();
        var provider = host.Services.GetRequiredService<IMicroserviceAddressProvider>();

        var http = provider.Resolve("orders-service", MicroserviceProtocol.Http);
        var grpc = provider.Resolve("orders-service", MicroserviceProtocol.Grpc);

        Assert.Equal("https://default.example/", http.Address.AbsoluteUri);
        Assert.Equal("https://grpc.example/", grpc.Address.AbsoluteUri);
    }

    [Fact]
    public void ServicesTryUse_SkipsWhenDefaultAlreadyRegistered()
    {
        var services = CreateServices();
        services.UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://application.example")));

        services.TryUseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://fallback.example")));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IMicroserviceAddressProvider>();

        var result = provider.Resolve("orders-service", MicroserviceProtocol.Http);

        Assert.Equal("https://application.example/", result.Address.AbsoluteUri);
    }

    [Fact]
    public void ServicesUse_AfterServicesTryUse_OverridesFallback()
    {
        var services = CreateServices();
        services.TryUseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://fallback.example")));

        services.UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://application.example")));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IMicroserviceAddressProvider>();

        var result = provider.Resolve("orders-service", MicroserviceProtocol.Grpc);

        Assert.Equal("https://application.example/", result.Address.AbsoluteUri);
    }

    [Fact]
    public void TryUse_SetsDefaultResolverWhenNoneSelected()
    {
        var services = CreateServices();
        services.TryUseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
            new Uri("https://fallback.example")));

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IMicroserviceAddressProvider>();

        var result = provider.Resolve("orders-service", MicroserviceProtocol.Http);

        Assert.Equal("https://fallback.example/", result.Address.AbsoluteUri);
    }

    static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("Integration"));
        services.AddMicroserviceAddressing();
        return services;
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
