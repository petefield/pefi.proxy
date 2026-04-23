using PeFi.Proxy;
using PeFi.Proxy.Models;
using Xunit;

namespace PeFi.Proxy.Tests;

public class MappersTests
{
    [Fact]
    public void ToRouteConfig_MapsRouteIdAndClusterId()
    {
        var route = new PersistedRoute
        {
            Id = "api",
            RouteId = "api",
            ClusterId = "api-cluster",
            Host = "api.pefi.co.uk",
        };

        var result = route.ToRouteConfig();

        Assert.Equal("api", result.RouteId);
        Assert.Equal("api-cluster", result.ClusterId);
    }

    [Fact]
    public void ToRouteConfig_MapsHost()
    {
        var route = new PersistedRoute
        {
            Id = "api",
            RouteId = "api",
            ClusterId = "api",
            Host = "api.pefi.co.uk",
        };

        var result = route.ToRouteConfig();

        Assert.NotNull(result.Match.Hosts);
        Assert.Single(result.Match.Hosts);
        Assert.Equal("api.pefi.co.uk", result.Match.Hosts.First());
    }

    [Fact]
    public void ToRouteConfig_MapsNullPath()
    {
        var route = new PersistedRoute
        {
            Id = "api",
            RouteId = "api",
            ClusterId = "api",
            Host = "api.pefi.co.uk",
            Path = null
        };

        var result = route.ToRouteConfig();

        Assert.Null(result.Match.Path);
    }

    [Fact]
    public void ToRouteConfig_MapsPath()
    {
        var route = new PersistedRoute
        {
            Id = "api",
            RouteId = "api",
            ClusterId = "api",
            Host = "api.pefi.co.uk",
            Path = "/api/{**catch-all}"
        };

        var result = route.ToRouteConfig();

        Assert.Equal("/api/{**catch-all}", result.Match.Path);
    }

    [Fact]
    public void ToClusterConfig_MapsClusterIdAndDestination()
    {
        var cluster = new PersistedCluster
        {
            Id = "api",
            ClusterId = "api",
            Destinations = new Dictionary<string, string>
            {
                ["destination1"] = "http://host.docker.internal:8080"
            }
        };

        var result = cluster.ToClusterConfig();

        Assert.Equal("api", result.ClusterId);
        Assert.True(result.Destinations!.ContainsKey("destination1"));
        Assert.Equal("http://host.docker.internal:8080", result.Destinations["destination1"].Address);
    }

    [Theory]
    [InlineData("http://host.docker.internal:8080")]
    [InlineData("http://host.docker.internal:3000")]
    [InlineData("https://external.example.com")]
    public void ToClusterConfig_PreservesDestinationAddress(string address)
    {
        var cluster = new PersistedCluster
        {
            Id = "svc",
            ClusterId = "svc",
            Destinations = new Dictionary<string, string>
            {
                ["destination1"] = address
            }
        };

        var result = cluster.ToClusterConfig();

        Assert.Equal(address, result.Destinations!["destination1"].Address);
    }

    [Fact]
    public void ToClusterConfig_MapsMultipleDestinations()
    {
        var cluster = new PersistedCluster
        {
            Id = "multi",
            ClusterId = "multi",
            Destinations = new Dictionary<string, string>
            {
                ["dest1"] = "http://host.docker.internal:8080",
                ["dest2"] = "http://host.docker.internal:8081"
            }
        };

        var result = cluster.ToClusterConfig();

        Assert.Equal(2, result.Destinations!.Count);
        Assert.Equal("http://host.docker.internal:8080", result.Destinations["dest1"].Address);
        Assert.Equal("http://host.docker.internal:8081", result.Destinations["dest2"].Address);
    }
}
