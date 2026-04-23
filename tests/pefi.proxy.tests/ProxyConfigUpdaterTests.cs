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
        dataStore.Get<PersistedCluster>(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(Enumerable.Empty<PersistedCluster>()));
        return dataStore;
    }

    [Fact]
    public async Task ExecuteAsync_WithNoPersistedData_UpdatesConfigWithEmptyCollections()
    {
        var configProvider = CreateConfigProvider();
        var updater = new ProxyConfig(NullLogger<ProxyConfig>.Instance, configProvider, EmptyDataStore());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await updater.StartAsync(cts.Token);
        await Task.Delay(100);
        await updater.StopAsync(CancellationToken.None);

        var config = configProvider.GetConfig();
        Assert.Empty(config.Routes);
        Assert.Empty(config.Clusters);
    }

    [Fact]
    public async Task ExecuteAsync_LoadsPersistedRoutesFromDatabase()
    {
        var dataStore = Substitute.For<IDataStore>();
        dataStore.Get<PersistedRoute>(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<IEnumerable<PersistedRoute>>(
            [
                new PersistedRoute { Id = "api", RouteId = "api", ClusterId = "api-cluster", Host = "api.pefi.co.uk" }
            ]));
        dataStore.Get<PersistedCluster>(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<IEnumerable<PersistedCluster>>(
            [
                new PersistedCluster
                {
                    Id = "api-cluster",
                    ClusterId = "api-cluster",
                    Destinations = new Dictionary<string, string> { ["destination1"] = "http://host.docker.internal:8080" }
                }
            ]));

        var configProvider = CreateConfigProvider();
        var updater = new ProxyConfig(NullLogger<ProxyConfig>.Instance, configProvider, dataStore);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await updater.StartAsync(cts.Token);
        await Task.Delay(100);
        await updater.StopAsync(CancellationToken.None);

        var config = configProvider.GetConfig();
        Assert.Single(config.Routes);
        Assert.Equal("api", config.Routes[0].RouteId);
        Assert.Single(config.Clusters);
        Assert.Equal("api-cluster", config.Clusters[0].ClusterId);
    }

    [Fact]
    public async Task ExecuteAsync_LoadsMultiplePersistedRoutes()
    {
        var dataStore = Substitute.For<IDataStore>();
        dataStore.Get<PersistedRoute>(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<IEnumerable<PersistedRoute>>(
            [
                new PersistedRoute { Id = "api", RouteId = "api", ClusterId = "api", Host = "api.pefi.co.uk" },
                new PersistedRoute { Id = "web", RouteId = "web", ClusterId = "web", Host = "web.pefi.co.uk" }
            ]));
        dataStore.Get<PersistedCluster>(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<IEnumerable<PersistedCluster>>(
            [
                new PersistedCluster { Id = "api", ClusterId = "api", Destinations = new() { ["d1"] = "http://host.docker.internal:8080" } },
                new PersistedCluster { Id = "web", ClusterId = "web", Destinations = new() { ["d1"] = "http://host.docker.internal:3000" } }
            ]));

        var configProvider = CreateConfigProvider();
        var updater = new ProxyConfig(NullLogger<ProxyConfig>.Instance, configProvider, dataStore);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await updater.StartAsync(cts.Token);
        await Task.Delay(100);
        await updater.StopAsync(CancellationToken.None);

        var config = configProvider.GetConfig();
        Assert.Equal(2, config.Routes.Count);
        Assert.Equal(2, config.Clusters.Count);
    }

    [Fact]
    public async Task ExecuteAsync_QueriesCorrectDatabaseAndCollection()
    {
        var dataStore = EmptyDataStore();
        var configProvider = CreateConfigProvider();
        var updater = new ProxyConfig(NullLogger<ProxyConfig>.Instance, configProvider, dataStore);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await updater.StartAsync(cts.Token);
        await Task.Delay(100);
        await updater.StopAsync(CancellationToken.None);

        await dataStore.Received(1).Get<PersistedRoute>("pefi", "routes");
        await dataStore.Received(1).Get<PersistedCluster>("pefi", "clusters");
    }
}
