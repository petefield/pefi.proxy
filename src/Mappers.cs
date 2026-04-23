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

    public static ClusterConfig ToClusterConfig(this PersistedRoute route) =>
        new ClusterConfig
        {
            ClusterId = route.ClusterId,
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["destination1"] = new() { Address = route.DestinationAddress }
            }
        };
}
