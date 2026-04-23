using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using pefi.persistence;
using PeFi.Proxy.Models;
using Xunit;

namespace PeFi.Proxy.Tests;

public class ConfigEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ConfigEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MongoDB:ConnectionString"] = "mongodb://localhost:27017",
                });
            });

            builder.ConfigureServices(services =>
            {
                var dataStore = Substitute.For<IDataStore>();
                dataStore.Get<PersistedRoute>(Arg.Any<string>(), Arg.Any<string>())
                    .Returns(Task.FromResult(Enumerable.Empty<PersistedRoute>()));
                dataStore.Get<PersistedCluster>(Arg.Any<string>(), Arg.Any<string>())
                    .Returns(Task.FromResult(Enumerable.Empty<PersistedCluster>()));
                dataStore.Add<PersistedRoute>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PersistedRoute>())
                    .Returns(Task.FromResult(new PersistedRoute()));
                dataStore.Add<PersistedCluster>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PersistedCluster>())
                    .Returns(Task.FromResult(new PersistedCluster()));
                services.AddSingleton(dataStore);
            });
        });
    }

    [Fact]
    public async Task GetConfig_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetConfig_ReturnsJsonWithRoutesAndClusters()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/config");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ConfigResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.routes);
        Assert.NotNull(body.clusters);
    }

    [Fact]
    public async Task GetConfig_IncludesStaticRoutesFromAppSettings()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/config");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ConfigResponse>();
        Assert.NotNull(body);
        Assert.Contains(body.routes, r => r.routeId == "immich");
    }

    [Fact]
    public async Task GetConfig_ContentTypeIsJson()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/config");
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateCluster_ReturnsCreated()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/clusters", new
        {
            clusterId = "test-cluster",
            destinations = new Dictionary<string, string> { ["destination1"] = "http://host.docker.internal:9090" }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateCluster_AppearsInConfig()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/clusters", new
        {
            clusterId = "cluster-for-config-test",
            destinations = new Dictionary<string, string> { ["destination1"] = "http://host.docker.internal:9091" }
        });

        var configResponse = await client.GetAsync("/config");
        var body = await configResponse.Content.ReadFromJsonAsync<ConfigResponse>();
        Assert.NotNull(body);
        Assert.Contains(body.clusters, c => c.clusterId == "cluster-for-config-test");
    }

    [Fact]
    public async Task CreateRoute_WithExistingCluster_AddsRouteToConfig()
    {
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/clusters", new
        {
            clusterId = "my-cluster",
            destinations = new Dictionary<string, string> { ["destination1"] = "http://host.docker.internal:7070" }
        });

        var createResponse = await client.PostAsJsonAsync("/routes", new
        {
            routeId = "new-route",
            clusterId = "my-cluster",
            host = "new-route.pefi.co.uk",
            path = "/api/{**catch-all}"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var configResponse = await client.GetAsync("/config");
        var body = await configResponse.Content.ReadFromJsonAsync<ConfigResponse>();
        Assert.NotNull(body);
        Assert.Contains(body.routes, r => r.routeId == "new-route" && r.clusterId == "my-cluster");
    }

    [Fact]
    public async Task CreateRoute_WithNonExistentCluster_ReturnsUnprocessableEntity()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/routes", new
        {
            routeId = "orphan-route",
            clusterId = "does-not-exist",
            host = "orphan.pefi.co.uk"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateRoute_WithExistingRouteId_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        // "immich" route exists in appsettings.json; its cluster also exists there
        var response = await client.PostAsJsonAsync("/routes", new
        {
            routeId = "immich",
            clusterId = "immich",
            host = "duplicate.pefi.co.uk"
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateCluster_WithoutDestinations_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/clusters", new
        {
            clusterId = "empty-cluster",
            destinations = new Dictionary<string, string>()
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Simple response models for deserialization
    private record ConfigResponse(RouteEntry[] routes, ClusterEntry[] clusters);
    private record RouteEntry(string routeId, string clusterId);
    private record ClusterEntry(string clusterId);
}
