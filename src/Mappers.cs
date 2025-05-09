using Yarp.ReverseProxy.Configuration;

namespace PeFi.Proxy;
public static class  Mappers
{
    public static RouteConfig ToRouteConfig(this ServiceDescription service)
    {
        return new RouteConfig
        {
            RouteId = service.ServiceName,
            ClusterId = service.ServiceName,
            Match = new RouteMatch
            {
                Hosts = [ service.HostName ],
            }
        };
    }


    public static ClusterConfig ToClusterConfig(this ServiceDescription service)
    {
        return new ClusterConfig
        {
            ClusterId = service.ServiceName,
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { service.ServiceName, new DestinationConfig() { Address = $"http://host.docker.internal:{service.HostPortNumber}" } }
            }
        };
    }
}