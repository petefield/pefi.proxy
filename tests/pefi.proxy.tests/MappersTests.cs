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
            DestinationAddress = "http://host.docker.internal:8080"
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
            DestinationAddress = "http://host.docker.internal:8080"
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
            DestinationAddress = "http://host.docker.internal:8080",
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
            DestinationAddress = "http://host.docker.internal:8080",
            Path = "/api/{**catch-all}"
        };

        var result = route.ToRouteConfig();

        Assert.Equal("/api/{**catch-all}", result.Match.Path);
    }

    [Fact]
    public void ToClusterConfig_MapsClusterIdAndDestination()
    {
        var route = new PersistedRoute
        {
            Id = "api",
            RouteId = "api",
            ClusterId = "api",
            Host = "api.pefi.co.uk",
            DestinationAddress = "http://host.docker.internal:8080"
        };

        var result = route.ToClusterConfig();

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
        var route = new PersistedRoute
        {
            Id = "svc",
            RouteId = "svc",
            ClusterId = "svc",
            Host = "svc.pefi.co.uk",
            DestinationAddress = address
        };

        var result = route.ToClusterConfig();

        Assert.Equal(address, result.Destinations!["destination1"].Address);
    }
}
