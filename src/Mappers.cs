using pefi.dynamicdns.Services;
using Yarp.ReverseProxy.Configuration;

namespace PeFi.Proxy;
public static class  Mappers
{
    public static RouteConfig? ToRouteConfig(this GetServiceResponse service)
    {
        if (service.hostName == null)
            return null;

        return new RouteConfig
        {
            RouteId = service.serviceName,
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
            ClusterId = service.serviceName,
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { service.serviceName, new DestinationConfig() { Address = $"http://192.168.1.86:{service.hostPortNumber}" } }
            }
        };
    }
}