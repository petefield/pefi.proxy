using pefi.persistence;
using PeFi.Proxy.Models;
using Yarp.ReverseProxy.Configuration;

namespace PeFi.Proxy;

public class ProxyConfig(ILogger<ProxyConfig> logger,
    InMemoryConfigProvider configProvider,
    IDataStore dataStore) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Loading persisted routes and clusters from database.");
        await UpdateConfig();
    }

    private async Task UpdateConfig()
    {
        var persistedRoutes = (await dataStore.Get<PersistedRoute>("pefi", "routes")).ToList();
        var persistedClusters = (await dataStore.Get<PersistedCluster>("pefi", "clusters")).ToList();

        var routes = persistedRoutes.Select(p => p.ToRouteConfig()).ToList();
        var clusters = persistedClusters.Select(c => c.ToClusterConfig()).ToList();

        configProvider.Update(routes, clusters);
    }
}
