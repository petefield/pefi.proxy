using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using pefi.persistence;
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
    public async Task ExecuteAsync_WithNoPersistedRoutes_UpdatesConfigWithEmptyCollections()
    {
        // Arrange
        var configProvider = CreateConfigProvider();
        var updater = new ProxyConfig(NullLogger<ProxyConfig>.Instance, configProvider, EmptyDataStore());

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await updater.StartAsync(cts.Token);
        await Task.Delay(100);
        await updater.StopAsync(CancellationToken.None);

        // Assert
        var config = configProvider.GetConfig();
        Assert.Empty(config.Routes);
        Assert.Empty(config.Clusters);
    }

    [Fact]
    public async Task ExecuteAsync_LoadsPersistedRoutesFromDatabase()
    {
        // Arrange
        var dataStore = Substitute.For<IDataStore>();
        dataStore.Get<PersistedRoute>(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<IEnumerable<PersistedRoute>>(
            [
                new PersistedRoute
                {
                    Id = "api",
                    RouteId = "api",
                    ClusterId = "api",
                    Host = "api.pefi.co.uk",
                    DestinationAddress = "http://host.docker.internal:8080"
                }
            ]));

        var configProvider = CreateConfigProvider();
        var updater = new ProxyConfig(NullLogger<ProxyConfig>.Instance, configProvider, dataStore);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await updater.StartAsync(cts.Token);
        await Task.Delay(100);
        await updater.StopAsync(CancellationToken.None);

        // Assert
        var config = configProvider.GetConfig();
        Assert.Single(config.Routes);
        Assert.Equal("api", config.Routes[0].RouteId);
        Assert.Single(config.Clusters);
        Assert.Equal("api", config.Clusters[0].ClusterId);
    }

    [Fact]
    public async Task ExecuteAsync_LoadsMultiplePersistedRoutes()
    {
        // Arrange
        var dataStore = Substitute.For<IDataStore>();
        dataStore.Get<PersistedRoute>(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<IEnumerable<PersistedRoute>>(
            [
                new PersistedRoute { Id = "api", RouteId = "api", ClusterId = "api", Host = "api.pefi.co.uk", DestinationAddress = "http://host.docker.internal:8080" },
                new PersistedRoute { Id = "web", RouteId = "web", ClusterId = "web", Host = "web.pefi.co.uk", DestinationAddress = "http://host.docker.internal:3000" }
            ]));

        var configProvider = CreateConfigProvider();
        var updater = new ProxyConfig(NullLogger<ProxyConfig>.Instance, configProvider, dataStore);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await updater.StartAsync(cts.Token);
        await Task.Delay(100);
        await updater.StopAsync(CancellationToken.None);

        // Assert
        var config = configProvider.GetConfig();
        Assert.Equal(2, config.Routes.Count);
        Assert.Equal(2, config.Clusters.Count);
    }

    [Fact]
    public async Task ExecuteAsync_QueriesCorrectDatabaseAndCollection()
    {
        // Arrange
        var dataStore = Substitute.For<IDataStore>();
        dataStore.Get<PersistedRoute>(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(Enumerable.Empty<PersistedRoute>()));

        var configProvider = CreateConfigProvider();
        var updater = new ProxyConfig(NullLogger<ProxyConfig>.Instance, configProvider, dataStore);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await updater.StartAsync(cts.Token);
        await Task.Delay(100);
        await updater.StopAsync(CancellationToken.None);

        // Assert
        await dataStore.Received(1).Get<PersistedRoute>("pefi", "routes");
    }
}
