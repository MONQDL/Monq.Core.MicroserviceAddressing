using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Monq.Core.MicroserviceAddressing.Addressing;
using Monq.Core.MicroserviceAddressing.DependencyInjection;
using Monq.Core.MicroserviceAddressing.Hosting;

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
        using var host = new HostBuilder()
            .UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://default.example")))
            .UseGrpcMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://grpc.example")))
            .Build();
        var provider = host.Services.GetRequiredService<IMicroserviceAddressProvider>();

        var http = provider.Resolve("orders-service", MicroserviceProtocol.Http);
        var grpc = provider.Resolve("orders-service", MicroserviceProtocol.Grpc);

        Assert.Equal("https://default.example/", http.Address.AbsoluteUri);
        Assert.Equal("https://grpc.example/", grpc.Address.AbsoluteUri);
    }

    [Fact]
    public void ClientResolver_OverridesProtocolAndDefaultResolvers()
    {
        using var host = new HostBuilder()
            .UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://default.example")))
            .UseHttpMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://http.example")))
            .Build();
        var provider = host.Services.GetRequiredService<IMicroserviceAddressProvider>();

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
        using var host = new HostBuilder()
            .UseHttpMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://first.example")))
            .UseHttpMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://second.example")))
            .Build();
        var provider = host.Services.GetRequiredService<IMicroserviceAddressProvider>();

        var result = provider.Resolve("orders-service", MicroserviceProtocol.Http);

        Assert.Equal("https://second.example/", result.Address.AbsoluteUri);
    }

    [Fact]
    public void LastGrpcRegistrationWins()
    {
        using var host = new HostBuilder()
            .UseGrpcMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://first.example")))
            .UseGrpcMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://second.example")))
            .Build();
        var provider = host.Services.GetRequiredService<IMicroserviceAddressProvider>();

        var result = provider.Resolve("orders-service", MicroserviceProtocol.Grpc);

        Assert.Equal("https://second.example/", result.Address.AbsoluteUri);
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
    public void HostExtension_ResolvesClientResolverOverProtocolAndDefault()
    {
        var hostBuilder = new HostBuilder()
            .UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://default.example")))
            .UseHttpMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://http.example")));

        var result = hostBuilder.ResolveMicroserviceAddress(
            CreateHostBuilderContext(),
            "orders-service",
            MicroserviceProtocol.Http,
            addressResolver: new FixedMicroserviceAddressResolver(
                new Uri("https://client.example")));

        Assert.Equal("https://client.example/", result.Address.AbsoluteUri);
    }

    [Fact]
    public void HostExtension_ThrowsOnUnsupportedProtocol()
    {
        var hostBuilder = new HostBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            hostBuilder.ResolveMicroserviceAddress(
                CreateHostBuilderContext(),
                "orders-service",
                (MicroserviceProtocol)99));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HostExtension_ThrowsOnEmptyApplicationName(string? applicationName)
    {
        var hostBuilder = new HostBuilder();

        Assert.Throws<ArgumentException>(() =>
            hostBuilder.ResolveMicroserviceAddress(
                CreateHostBuilderContext(),
                applicationName!,
                MicroserviceProtocol.Http));
    }

    [Fact]
    public void ResolverFactory_ReturningNull_ThrowsOnResolution()
    {
        var hostBuilder = new HostBuilder()
            .UseMicroserviceAddressResolver(_ => null!);

        Assert.Throws<InvalidOperationException>(() =>
            hostBuilder.ResolveMicroserviceAddress(
                CreateHostBuilderContext(),
                "orders-service",
                MicroserviceProtocol.Http));
    }

    [Fact]
    public void HostExtension_ConcurrentResolution_InvokesFactoryOnce()
    {
        var invocationCount = 0;
        var hostBuilder = new HostBuilder()
            .UseGrpcMicroserviceAddressResolver(_ =>
            {
                Interlocked.Increment(ref invocationCount);
                return new FixedMicroserviceAddressResolver(new Uri("https://grpc.example"));
            });
        var context = CreateHostBuilderContext();

        Parallel.For(0, 32, _ =>
        {
            var result = hostBuilder.ResolveMicroserviceAddress(
                context,
                "orders-service",
                MicroserviceProtocol.Grpc);
            Assert.Equal("https://grpc.example/", result.Address.AbsoluteUri);
        });

        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public void LastDefaultRegistrationWins()
    {
        using var host = new HostBuilder()
            .UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://first.example")))
            .UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://second.example")))
            .Build();
        var provider = host.Services.GetRequiredService<IMicroserviceAddressProvider>();

        var http = provider.Resolve("orders-service", MicroserviceProtocol.Http);
        var grpc = provider.Resolve("orders-service", MicroserviceProtocol.Grpc);

        Assert.Equal("https://second.example/", http.Address.AbsoluteUri);
        Assert.Equal("https://second.example/", grpc.Address.AbsoluteUri);
    }

    [Fact]
    public void TryUse_SetsDefaultResolverWhenNoneSelected()
    {
        using var host = new HostBuilder()
            .TryUseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://fallback.example")))
            .Build();
        var provider = host.Services.GetRequiredService<IMicroserviceAddressProvider>();

        var result = provider.Resolve("orders-service", MicroserviceProtocol.Http);

        Assert.Equal("https://fallback.example/", result.Address.AbsoluteUri);
    }

    [Fact]
    public void TryUse_DoesNotReplaceEarlierDefaultResolver()
    {
        var factoryInvocationCount = 0;
        using var host = new HostBuilder()
            .UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://application.example")))
            .TryUseMicroserviceAddressResolver(_ =>
            {
                factoryInvocationCount++;
                return new FixedMicroserviceAddressResolver(
                    new Uri("https://fallback.example"));
            })
            .Build();
        var provider = host.Services.GetRequiredService<IMicroserviceAddressProvider>();

        var result = provider.Resolve("orders-service", MicroserviceProtocol.Http);

        Assert.Equal("https://application.example/", result.Address.AbsoluteUri);
        Assert.Equal(0, factoryInvocationCount);
    }

    [Fact]
    public void UseAfterTryUse_OverridesFallbackResolver()
    {
        using var host = new HostBuilder()
            .TryUseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://fallback.example")))
            .UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://application.example")))
            .Build();
        var provider = host.Services.GetRequiredService<IMicroserviceAddressProvider>();

        var result = provider.Resolve("orders-service", MicroserviceProtocol.Grpc);

        Assert.Equal("https://application.example/", result.Address.AbsoluteUri);
    }

    [Fact]
    public void HostResolverSelection_IsSharedWithDependencyInjectionProvider()
    {
        ResolvedMicroserviceAddress? registrationAddress = null;
        var hostBuilder = new HostBuilder()
            .UseMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://default.example")))
            .UseGrpcMicroserviceAddressResolver(new FixedMicroserviceAddressResolver(
                new Uri("https://grpc.example")));

        hostBuilder.ConfigureServices((context, _) =>
        {
            registrationAddress = hostBuilder
                .ResolveMicroserviceAddress(
                    context,
                    "orders-service",
                    MicroserviceProtocol.Grpc);
        });

        using var host = hostBuilder.Build();
        var runtimeAddress = host.Services
            .GetRequiredService<IMicroserviceAddressProvider>()
            .Resolve("orders-service", MicroserviceProtocol.Grpc);

        Assert.NotNull(registrationAddress);
        Assert.Equal("https://grpc.example/", registrationAddress.Address.AbsoluteUri);
        Assert.Equal(registrationAddress, runtimeAddress);
    }

    [Fact]
    public void ResolverFactory_IsInvokedOnceForRegistrationAndRuntimeResolution()
    {
        var invocationCount = 0;
        var resolver = new FixedMicroserviceAddressResolver(
            new Uri("https://http.example"));
        ResolvedMicroserviceAddress? registrationAddress = null;
        var hostBuilder = new HostBuilder()
            .UseHttpMicroserviceAddressResolver(_ =>
            {
                invocationCount++;
                return resolver;
            });

        hostBuilder.ConfigureServices((context, _) =>
        {
            registrationAddress = hostBuilder.ResolveMicroserviceAddress(
                context,
                "orders-service",
                MicroserviceProtocol.Http);
        });

        using var host = hostBuilder.Build();
        var runtimeAddress = host.Services
            .GetRequiredService<IMicroserviceAddressProvider>()
            .Resolve("orders-service", MicroserviceProtocol.Http);

        Assert.Equal(1, invocationCount);
        Assert.NotNull(registrationAddress);
        Assert.Equal(registrationAddress, runtimeAddress);
    }

    static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("Integration"));
        services.AddMicroserviceAddressing();
        return services;
    }

    static HostBuilderContext CreateHostBuilderContext() =>
        new(new Dictionary<object, object>())
        {
            Configuration = new ConfigurationBuilder().Build(),
            HostingEnvironment = new TestHostEnvironment("Integration"),
        };

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
