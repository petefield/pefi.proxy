using pefi.persistence;
using PeFi.Proxy;
using PeFi.Proxy.Models;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
builder.Logging.AddConsole();

var tlsCertificateSelector = TlsCertificateSelector.FromDirectory(builder.Configuration.GetSection("Tls"));
var httpPort = builder.Configuration.GetValue<int?>("HTTP_PORT") ?? 8080;
var httpsPort = builder.Configuration.GetValue<int?>("HTTPS_PORT")
    ?? builder.Configuration.GetValue<int?>("Tls:Port")
    ?? 8443;

builder.WebHost.ConfigureKestrel(options =>
{
    // Always accept HTTP
    options.ListenAnyIP(httpPort);

    // Add HTTPS only when certs are configured
    if (tlsCertificateSelector.HasCertificates)
    {
        options.ConfigureHttpsDefaults(httpsOptions =>
        {
            Console.WriteLine("TLS certificates loaded. HTTPS will be enabled.");
            httpsOptions.ServerCertificateSelector = (_, serverName) =>{
                Console.WriteLine($"TLS certificates Select : {serverName}.");
                return tlsCertificateSelector.Select(serverName);
            };
        });

        options.ListenAnyIP(httpsPort, listenOptions => listenOptions.UseHttps());
    }
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddReverseProxy()
    .LoadFromMemory([], [])
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddPeFiPersistance(options => {
    options.ConnectionString = builder.Configuration.GetValue<string>("MongoDB:ConnectionString") ?? "";
});

builder.Services.AddHostedService<ProxyConfig>();

var app = builder.Build();
if (tlsCertificateSelector.HasCertificates)
    app.Lifetime.ApplicationStopping.Register(tlsCertificateSelector.Dispose);

app.MapGet("/config", (InMemoryConfigProvider memoryConfigProvider, IProxyConfigProvider appSettingsConfigProvider) => {
    var appSettingsConfig = appSettingsConfigProvider.GetConfig();
    var memoryConfig = memoryConfigProvider.GetConfig();

    return new
    {
        Routes = appSettingsConfig.Routes.Concat(memoryConfig.Routes),
        Clusters = appSettingsConfig.Clusters.Concat(memoryConfig.Clusters),
    };
}).WithName("Get Current Config")
.WithOpenApi();

app.MapGet("/clusters", async (IDataStore dataStore) =>
{
    var clusters = await dataStore.Get<PersistedCluster>("pefi", "clusters");
    return Results.Ok(clusters);
}).WithName("Get Clusters")
.WithOpenApi();

app.MapPost("/clusters", async (CreateClusterRequest request, InMemoryConfigProvider memoryConfigProvider, IProxyConfigProvider appSettingsConfigProvider, IDataStore dataStore) =>
{
    if (string.IsNullOrWhiteSpace(request.ClusterId))
        return Results.BadRequest(new { error = "clusterId is required." });

    if (request.Destinations is null || request.Destinations.Count == 0)
        return Results.BadRequest(new { error = "At least one destination is required." });

    foreach (var (key, address) in request.Destinations)
    {
        if (string.IsNullOrWhiteSpace(address))
            return Results.BadRequest(new { error = $"Destination '{key}' has an empty address." });
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return Results.BadRequest(new { error = $"Destination '{key}' must be an absolute http/https URL." });
    }

    var clusterId = request.ClusterId.Trim();

    var appSettingsConfig = appSettingsConfigProvider.GetConfig();
    var memoryConfig = memoryConfigProvider.GetConfig();

    if (appSettingsConfig.Clusters.Any(c => string.Equals(c.ClusterId, clusterId, StringComparison.OrdinalIgnoreCase))
        || memoryConfig.Clusters.Any(c => string.Equals(c.ClusterId, clusterId, StringComparison.OrdinalIgnoreCase)))
        return Results.Conflict(new { error = $"A cluster with id '{clusterId}' already exists." });

    var destinations = request.Destinations
        .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim().TrimEnd('/'));

    var persisted = new PersistedCluster
    {
        Id = clusterId,
        ClusterId = clusterId,
        Destinations = destinations
    };

    await dataStore.Add<PersistedCluster>("pefi", "clusters", persisted);

    var clusters = memoryConfig.Clusters.ToList();
    clusters.Add(persisted.ToClusterConfig());
    var routes = memoryConfig.Routes.ToList();
    memoryConfigProvider.Update(routes, clusters);

    return Results.Created($"/clusters/{clusterId}", persisted);
})
.WithName("Create Cluster")
.WithOpenApi();

app.MapPost("/routes", async (CreateRouteRequest request, InMemoryConfigProvider memoryConfigProvider, IProxyConfigProvider appSettingsConfigProvider, IDataStore dataStore) =>
{
    if (string.IsNullOrWhiteSpace(request.RouteId))
        return Results.BadRequest(new { error = "routeId is required." });

    if (string.IsNullOrWhiteSpace(request.Host))
        return Results.BadRequest(new { error = "host is required." });

    if (string.IsNullOrWhiteSpace(request.ClusterId))
        return Results.BadRequest(new { error = "clusterId is required." });

    var routeId = request.RouteId.Trim();
    var clusterId = request.ClusterId.Trim();
    var host = request.Host.Trim();
    var path = string.IsNullOrWhiteSpace(request.Path) ? null : request.Path.Trim();

    var appSettingsConfig = appSettingsConfigProvider.GetConfig();
    var memoryConfig = memoryConfigProvider.GetConfig();

    if (appSettingsConfig.Routes.Any(r => string.Equals(r.RouteId, routeId, StringComparison.OrdinalIgnoreCase))
        || memoryConfig.Routes.Any(r => string.Equals(r.RouteId, routeId, StringComparison.OrdinalIgnoreCase)))
        return Results.Conflict(new { error = $"A route with id '{routeId}' already exists." });

    // Cluster must already exist
    var clusterExists =
        appSettingsConfig.Clusters.Any(c => string.Equals(c.ClusterId, clusterId, StringComparison.OrdinalIgnoreCase))
        || memoryConfig.Clusters.Any(c => string.Equals(c.ClusterId, clusterId, StringComparison.OrdinalIgnoreCase));

    if (!clusterExists)
        return Results.UnprocessableEntity(new { error = $"Cluster '{clusterId}' does not exist. Create it first." });

    var routes = memoryConfig.Routes.ToList();
    routes.Add(new RouteConfig
    {
        RouteId = routeId,
        ClusterId = clusterId,
        Match = new RouteMatch
        {
            Hosts = [host],
            Path = path
        }
    });

    memoryConfigProvider.Update(routes, memoryConfig.Clusters.ToList());

    await dataStore.Add<PersistedRoute>("pefi", "routes", new PersistedRoute
    {
        Id = routeId,
        RouteId = routeId,
        ClusterId = clusterId,
        Host = host,
        Path = path
    });

    return Results.Created($"/routes/{routeId}", new CreateRouteResponse(routeId, clusterId, host, path));
})
.WithName("Create Route")
.WithOpenApi();

app.UseBlazorFrameworkFiles("/dashboard");
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.MapReverseProxy();

app.MapFallbackToFile("/dashboard/{**path:nonfile}", "dashboard/index.html");
app.MapFallbackToFile("/dashboard", "dashboard/index.html");
app.Run();

public record CreateClusterRequest(string? ClusterId, Dictionary<string, string>? Destinations);
public record CreateRouteRequest(string? RouteId, string? ClusterId, string? Host, string? Path);
public record CreateRouteResponse(string RouteId, string ClusterId, string Host, string? Path);

public partial class Program { }
