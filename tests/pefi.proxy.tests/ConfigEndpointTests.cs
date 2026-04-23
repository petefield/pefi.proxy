using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using pefi.dynamicdns.Services;
using pefi.persistence;
using pefi.Rabbit;
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
                    ["ServiceManager:baseurl"] = "http://test-service-manager",
                    ["Messaging:address"] = "localhost",
                    ["Messaging:username"] = "guest",
                    ["Messaging:password"] = "guest",
                    ["MongoDB:ConnectionString"] = "mongodb://localhost:27017",
                });
            });

            builder.ConfigureServices(services =>
            {
                // Replace IMessageBroker with a mock to avoid connecting to RabbitMQ
                var messageBroker = Substitute.For<IMessageBroker>();
                var topic = Substitute.For<ITopic>();
                messageBroker.CreateTopic(Arg.Any<string>()).Returns(Task.FromResult(topic));
                services.Replace(ServiceDescriptor.Singleton(messageBroker));

                // Replace the HTTP client handler for ServiceManagerClient to avoid
                // making real network calls to the Service Manager
                services.AddHttpClient<ServiceManagerClient>()
                    .ConfigurePrimaryHttpMessageHandler(
                        () => new MockHttpMessageHandler([]));

                // Replace IDataStore with a mock to avoid connecting to MongoDB
                var dataStore = Substitute.For<IDataStore>();
                dataStore.Get<PersistedRoute>(Arg.Any<System.Linq.Expressions.Expression<Func<PersistedRoute, bool>>>())
                    .Returns(Task.FromResult(Enumerable.Empty<PersistedRoute>()));
                dataStore.Add<PersistedRoute>(Arg.Any<PersistedRoute>()).Returns(Task.CompletedTask);
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
        // The static route "immich" is defined in appsettings.json
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
    public async Task CreateRoute_AddsRouteAndClusterToConfig()
    {
        var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/routes", new
        {
            routeId = "new-route",
            host = "new-route.pefi.co.uk",
            destinationAddress = "http://host.docker.internal:7070",
            path = "/api/{**catch-all}"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var configResponse = await client.GetAsync("/config");
        configResponse.EnsureSuccessStatusCode();
        var body = await configResponse.Content.ReadFromJsonAsync<ConfigResponse>();

        Assert.NotNull(body);
        Assert.Contains(body.routes, r => r.routeId == "new-route" && r.clusterId == "new-route");
        Assert.Contains(body.clusters, c => c.clusterId == "new-route");
    }

    [Fact]
    public async Task CreateRoute_WithExistingRouteId_ReturnsConflict()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/routes", new
        {
            routeId = "immich",
            host = "duplicate.pefi.co.uk",
            destinationAddress = "http://host.docker.internal:6060"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // Simple response model for deserialization
    private record ConfigResponse(
        RouteEntry[] routes,
        ClusterEntry[] clusters);

    private record RouteEntry(string routeId, string clusterId);
    private record ClusterEntry(string clusterId);
}
