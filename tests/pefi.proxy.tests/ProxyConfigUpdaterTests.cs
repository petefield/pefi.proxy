using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using pefi.dynamicdns.Services;
using pefi.persistence;
using pefi.Rabbit;
using PeFi.Proxy;
using PeFi.Proxy.Models;
using Yarp.ReverseProxy.Configuration;
using Xunit;

namespace PeFi.Proxy.Tests;

public class ProxyConfigUpdaterTests
{
    private static InMemoryConfigProvider CreateConfigProvider()
    {
        var services = new ServiceCollection();
        services.AddReverseProxy().LoadFromMemory([], []);
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<InMemoryConfigProvider>();
    }

    private static IDataStore EmptyDataStore()
    {
        var dataStore = Substitute.For<IDataStore>();
        dataStore.Get<PersistedRoute>(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(Enumerable.Empty<PersistedRoute>()));
        return dataStore;
    }

    [Fact]
    public async Task ExecuteAsync_CreatesEventsTopic()
    {
        // Arrange
        var messageBroker = Substitute.For<IMessageBroker>();
        var topic = Substitute.For<ITopic>();
        messageBroker.CreateTopic("Events").Returns(Task.FromResult(topic));

        using var handler = new MockHttpMessageHandler([]);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var serviceManagerClient = new ServiceManagerClient(httpClient);
        var configProvider = CreateConfigProvider();

        var updater = new ProxyConfig(
            NullLogger<ProxyConfig>.Instance,
            messageBroker,
            configProvider,
            serviceManagerClient,
            EmptyDataStore());

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await updater.StartAsync(cts.Token);
        await Task.Delay(100);
        await updater.StopAsync(CancellationToken.None);

        // Assert
        await messageBroker.Received(1).CreateTopic("Events");
    }

    [Fact]
    public async Task ExecuteAsync_SubscribesToEventsServicePattern()
    {
        // Arrange
        var messageBroker = Substitute.For<IMessageBroker>();
        var topic = Substitute.For<ITopic>();
        messageBroker.CreateTopic("Events").Returns(Task.FromResult(topic));

        using var handler = new MockHttpMessageHandler([]);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var serviceManagerClient = new ServiceManagerClient(httpClient);
        var configProvider = CreateConfigProvider();

        var updater = new ProxyConfig(
            NullLogger<ProxyConfig>.Instance,
            messageBroker,
            configProvider,
            serviceManagerClient,
            EmptyDataStore());

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await updater.StartAsync(cts.Token);
        await Task.Delay(100);
        await updater.StopAsync(CancellationToken.None);

        // Assert
        await topic.Received(1).Subscribe(
            Arg.Is<string>(s => s == "events.service.#"),
            Arg.Any<Func<string, object, Task>>());
    }

    [Fact]
    public async Task ExecuteAsync_LoadsServicesOnStartup()
    {
        // Arrange
        var messageBroker = Substitute.For<IMessageBroker>();
        var topic = Substitute.For<ITopic>();
        messageBroker.CreateTopic("Events").Returns(Task.FromResult(topic));

        var services = new List<GetServiceResponse>
        {
            new() { serviceName = "api", hostName = "api", hostPortNumber = "8080" }
        };

        using var handler = new MockHttpMessageHandler(services);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var serviceManagerClient = new ServiceManagerClient(httpClient);
        var configProvider = CreateConfigProvider();

        var updater = new ProxyConfig(
            NullLogger<ProxyConfig>.Instance,
            messageBroker,
            configProvider,
            serviceManagerClient,
            EmptyDataStore());

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await updater.StartAsync(cts.Token);
        await Task.Delay(100);
        await updater.StopAsync(CancellationToken.None);

        // Assert - initial load should have hit the HTTP endpoint
        Assert.True(handler.RequestCount >= 1, "Should have called Get_All_ServicesAsync at least once on startup");
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesConfigProviderWithRoutes()
    {
        // Arrange
        var messageBroker = Substitute.For<IMessageBroker>();
        var topic = Substitute.For<ITopic>();
        messageBroker.CreateTopic("Events").Returns(Task.FromResult(topic));

        var services = new List<GetServiceResponse>
        {
            new() { serviceName = "api", hostName = "api", hostPortNumber = "8080" },
            new() { serviceName = "web", hostName = "web", hostPortNumber = "3000" }
        };

        using var handler = new MockHttpMessageHandler(services);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var serviceManagerClient = new ServiceManagerClient(httpClient);
        var configProvider = CreateConfigProvider();

        var updater = new ProxyConfig(
            NullLogger<ProxyConfig>.Instance,
            messageBroker,
            configProvider,
            serviceManagerClient,
            EmptyDataStore());

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await updater.StartAsync(cts.Token);
        await Task.Delay(200);
        await updater.StopAsync(CancellationToken.None);

        // Assert
        var config = configProvider.GetConfig();
        Assert.Equal(2, config.Routes.Count);
        Assert.Equal(2, config.Clusters.Count);
    }

    [Fact]
    public async Task ExecuteAsync_FiltersServicesWithNullHostName()
    {
        // Arrange
        var messageBroker = Substitute.For<IMessageBroker>();
        var topic = Substitute.For<ITopic>();
        messageBroker.CreateTopic("Events").Returns(Task.FromResult(topic));

        var services = new List<GetServiceResponse>
        {
            new() { serviceName = "api", hostName = "api", hostPortNumber = "8080" },
            new() { serviceName = "internal", hostName = null, hostPortNumber = "9090" }
        };

        using var handler = new MockHttpMessageHandler(services);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var serviceManagerClient = new ServiceManagerClient(httpClient);
        var configProvider = CreateConfigProvider();

        var updater = new ProxyConfig(
            NullLogger<ProxyConfig>.Instance,
            messageBroker,
            configProvider,
            serviceManagerClient,
            EmptyDataStore());

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await updater.StartAsync(cts.Token);
        await Task.Delay(200);
        await updater.StopAsync(CancellationToken.None);

        // Assert - only the service with a hostName should produce a route
        var config = configProvider.GetConfig();
        Assert.Single(config.Routes);
        Assert.Equal("api", config.Routes[0].RouteId);
    }

    [Fact]
    public async Task ExecuteAsync_FiltersServicesWithNullPortNumber()
    {
        // Arrange
        var messageBroker = Substitute.For<IMessageBroker>();
        var topic = Substitute.For<ITopic>();
        messageBroker.CreateTopic("Events").Returns(Task.FromResult(topic));

        var services = new List<GetServiceResponse>
        {
            new() { serviceName = "api", hostName = "api", hostPortNumber = "8080" },
            new() { serviceName = "proxy", hostName = "proxy", hostPortNumber = null }
        };

        using var handler = new MockHttpMessageHandler(services);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var serviceManagerClient = new ServiceManagerClient(httpClient);
        var configProvider = CreateConfigProvider();

        var updater = new ProxyConfig(
            NullLogger<ProxyConfig>.Instance,
            messageBroker,
            configProvider,
            serviceManagerClient,
            EmptyDataStore());

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await updater.StartAsync(cts.Token);
        await Task.Delay(200);
        await updater.StopAsync(CancellationToken.None);

        // Assert - only the service with a hostPortNumber should produce a cluster
        var config = configProvider.GetConfig();
        Assert.Single(config.Clusters);
        Assert.Equal("api", config.Clusters[0].ClusterId);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoServices_UpdatesConfigWithEmptyCollections()
    {
        // Arrange
        var messageBroker = Substitute.For<IMessageBroker>();
        var topic = Substitute.For<ITopic>();
        messageBroker.CreateTopic("Events").Returns(Task.FromResult(topic));

        using var handler = new MockHttpMessageHandler([]);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var serviceManagerClient = new ServiceManagerClient(httpClient);
        var configProvider = CreateConfigProvider();

        var updater = new ProxyConfig(
            NullLogger<ProxyConfig>.Instance,
            messageBroker,
            configProvider,
            serviceManagerClient,
            EmptyDataStore());

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await updater.StartAsync(cts.Token);
        await Task.Delay(200);
        await updater.StopAsync(CancellationToken.None);

        // Assert
        var config = configProvider.GetConfig();
        Assert.Empty(config.Routes);
        Assert.Empty(config.Clusters);
    }
}
