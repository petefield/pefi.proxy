using pefi.dynamicdns.Services;
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


    public static RouteConfig? ToRouteConfig(this GetServiceResponse service)
    {
        if (service.hostName == null)
            return null;

        return new RouteConfig
        {
            RouteId = service.serviceName!,
            ClusterId = service.serviceName,
            Match = new RouteMatch
            {
                Hosts = [ $"{service.hostName}.pefi.co.uk" ],
            }
        };
    }


    public static ClusterConfig? ToClusterConfig(this GetServiceResponse service)
    {

        if (service.hostPortNumber == null)
            return null;

        return new ClusterConfig
        {
            ClusterId = service.serviceName!,
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { service.serviceName!, new DestinationConfig() { Address = $"http://host.docker.internal:{service.hostPortNumber}" } }
            }
        };
    }
}