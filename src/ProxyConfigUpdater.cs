using pefi.dynamicdns.Services;
using pefi.Rabbit;
using Yarp.ReverseProxy.Configuration;

namespace PeFi.Proxy;

public class ProxyConfig(ILogger<ProxyConfig> logger,
    IMessageBroker messageBroker,
    InMemoryConfigProvider configProvider,
    ServiceManagerClient serviceManagerClient) : BackgroundService
{
    private ITopic? _topic;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Proxy Config Updater is listenting for new services.");

        _topic = await messageBroker.CreateTopic("Events");

        await _topic.Subscribe("events.service.#",  async(key, message) => { 
            await UpdateConfig();
        });
        await UpdateConfig();
    }

    private async Task UpdateConfig()
    {
        var allServices = await serviceManagerClient.Get_All_ServicesAsync();

        var routes = allServices
            .Select(serviceDescription => serviceDescription.ToRouteConfig())
            .Where(route => route != null)
            .Select(x => x!)
            .ToList();

        var clusters = allServices
            .Select(serviceDescription => serviceDescription.ToClusterConfig())
            .Where(cluster => cluster != null)
            .Select(x => x!)
            .ToList();

        if (routes != null && clusters != null)
            configProvider.Update(routes, clusters);
    }
}