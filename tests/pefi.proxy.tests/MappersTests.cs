using pefi.dynamicdns.Services;
using PeFi.Proxy;
using Xunit;

namespace PeFi.Proxy.Tests;

public class MappersTests
{
    [Fact]
    public void ToRouteConfig_WithValidHostName_ReturnsRouteConfig()
    {
        var service = new GetServiceResponse
        {
            serviceName = "test-service",
            hostName = "test-service",
            hostPortNumber = "8080"
        };

        var result = service.ToRouteConfig();

        Assert.NotNull(result);
        Assert.Equal("test-service", result.RouteId);
        Assert.Equal("test-service", result.ClusterId);
        Assert.NotNull(result.Match.Hosts);
        Assert.Single(result.Match.Hosts);
        Assert.Equal("test-service.pefi.co.uk", result.Match.Hosts.First());
    }

    [Fact]
    public void ToRouteConfig_WithNullHostName_ReturnsNull()
    {
        var service = new GetServiceResponse
        {
            serviceName = "test-service",
            hostName = null
        };

        var result = service.ToRouteConfig();

        Assert.Null(result);
    }

    [Fact]
    public void ToClusterConfig_WithValidPortNumber_ReturnsClusterConfig()
    {
        var service = new GetServiceResponse
        {
            serviceName = "test-service",
            hostPortNumber = "8080"
        };

        var result = service.ToClusterConfig();

        Assert.NotNull(result);
        Assert.Equal("test-service", result.ClusterId);
        Assert.NotNull(result.Destinations);
        Assert.True(result.Destinations.ContainsKey("test-service"));
        Assert.Equal("http://host.docker.internal:8080", result.Destinations["test-service"].Address);
    }

    [Fact]
    public void ToClusterConfig_WithNullPortNumber_ReturnsNull()
    {
        var service = new GetServiceResponse
        {
            serviceName = "test-service",
            hostPortNumber = null
        };

        var result = service.ToClusterConfig();

        Assert.Null(result);
    }

    [Theory]
    [InlineData("my-api", "my-api.pefi.co.uk")]
    [InlineData("payment-service", "payment-service.pefi.co.uk")]
    [InlineData("auth", "auth.pefi.co.uk")]
    public void ToRouteConfig_HostNameFormatsAsPefiDomain(string hostName, string expectedHost)
    {
        var service = new GetServiceResponse
        {
            serviceName = "service",
            hostName = hostName
        };

        var result = service.ToRouteConfig();

        Assert.NotNull(result);
        Assert.NotNull(result.Match.Hosts);
        Assert.Contains(expectedHost, result.Match.Hosts);
    }

    [Theory]
    [InlineData("8080", "http://host.docker.internal:8080")]
    [InlineData("3000", "http://host.docker.internal:3000")]
    [InlineData("443", "http://host.docker.internal:443")]
    public void ToClusterConfig_AddressFormatsWithDockerInternalHost(string portNumber, string expectedAddress)
    {
        var service = new GetServiceResponse
        {
            serviceName = "service",
            hostPortNumber = portNumber
        };

        var result = service.ToClusterConfig();

        Assert.NotNull(result);
        Assert.NotNull(result.Destinations);
        Assert.Equal(expectedAddress, result.Destinations["service"].Address);
    }

    [Fact]
    public void ToRouteConfig_RouteIdAndClusterIdMatchServiceName()
    {
        var service = new GetServiceResponse
        {
            serviceName = "my-unique-service",
            hostName = "my-unique-service"
        };

        var result = service.ToRouteConfig();

        Assert.NotNull(result);
        Assert.Equal(service.serviceName, result.RouteId);
        Assert.Equal(service.serviceName, result.ClusterId);
    }

    [Fact]
    public void ToClusterConfig_DestinationKeyMatchesServiceName()
    {
        var service = new GetServiceResponse
        {
            serviceName = "my-unique-service",
            hostPortNumber = "5000"
        };

        var result = service.ToClusterConfig();

        Assert.NotNull(result);
        Assert.NotNull(result.Destinations);
        Assert.True(result.Destinations.ContainsKey("my-unique-service"));
    }
}
