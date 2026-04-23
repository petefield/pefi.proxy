using PeFi.Proxy.Models;
using Yarp.ReverseProxy.Configuration;

namespace PeFi.Proxy;
public static class Mappers
{
    public static RouteConfig ToRouteConfig(this PersistedRoute route) =>
        new RouteConfig
        {
            RouteId = route.RouteId,
            ClusterId = route.ClusterId,
            Match = new RouteMatch
            {
                Hosts = [route.Host],
                Path = route.Path
            }
        };

    public static ClusterConfig ToClusterConfig(this PersistedCluster cluster) =>
        new ClusterConfig
        {
            ClusterId = cluster.ClusterId,
            Destinations = cluster.Destinations.ToDictionary(
                kvp => kvp.Key,
                kvp => new DestinationConfig { Address = kvp.Value },
                StringComparer.OrdinalIgnoreCase)
        };
}
