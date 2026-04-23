using pefi;
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

builder.Services.AddPeFiMessaging(options => {
    options.Username = builder.Configuration.GetSection("Messaging").GetValue<string>("username") ?? "";
    options.Password = builder.Configuration.GetSection("Messaging").GetValue<string>("password") ?? "";
    options.Address = builder.Configuration.GetSection("Messaging").GetValue<string>("address") ?? "";
});

builder.Services.AddPeFiPersistance(options => {
    options.ConnectionString = builder.Configuration.GetValue<string>("MongoDB:ConnectionString") ?? "";
});

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

app.MapPost("/routes", async (CreateRouteRequest request, InMemoryConfigProvider memoryConfigProvider, IProxyConfigProvider appSettingsConfigProvider, IDataStore dataStore) =>
{
    if (string.IsNullOrWhiteSpace(request.RouteId))
        return Results.BadRequest(new { error = "routeId is required." });

    if (string.IsNullOrWhiteSpace(request.Host))
        return Results.BadRequest(new { error = "host is required." });

    if (string.IsNullOrWhiteSpace(request.DestinationAddress))
        return Results.BadRequest(new { error = "destinationAddress is required." });

    if (!Uri.TryCreate(request.DestinationAddress, UriKind.Absolute, out var destinationUri)
        || (destinationUri.Scheme != Uri.UriSchemeHttp && destinationUri.Scheme != Uri.UriSchemeHttps))
        return Results.BadRequest(new { error = "destinationAddress must be an absolute http/https URL." });

    var routeId = request.RouteId.Trim();
    var clusterId = string.IsNullOrWhiteSpace(request.ClusterId) ? routeId : request.ClusterId.Trim();
    var host = request.Host.Trim();
    var destinationAddress = destinationUri.ToString().TrimEnd('/');
    var path = string.IsNullOrWhiteSpace(request.Path) ? null : request.Path.Trim();

    var appSettingsConfig = appSettingsConfigProvider.GetConfig();
    var memoryConfig = memoryConfigProvider.GetConfig();

    if (appSettingsConfig.Routes.Any(r => string.Equals(r.RouteId, routeId, StringComparison.OrdinalIgnoreCase))
        || memoryConfig.Routes.Any(r => string.Equals(r.RouteId, routeId, StringComparison.OrdinalIgnoreCase)))
        return Results.Conflict(new { error = $"A route with id '{routeId}' already exists." });

    if (appSettingsConfig.Clusters.Any(c => string.Equals(c.ClusterId, clusterId, StringComparison.OrdinalIgnoreCase))
        || memoryConfig.Clusters.Any(c => string.Equals(c.ClusterId, clusterId, StringComparison.OrdinalIgnoreCase)))
        return Results.Conflict(new { error = $"A cluster with id '{clusterId}' already exists." });

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

    var clusters = memoryConfig.Clusters.ToList();
    clusters.Add(new ClusterConfig
    {
        ClusterId = clusterId,
        Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["destination1"] = new() { Address = destinationAddress }
        }
    });

    memoryConfigProvider.Update(routes, clusters);

    await dataStore.Add<PersistedRoute>("routes", routeId, new PersistedRoute
    {
        Id = routeId,
        RouteId = routeId,
        ClusterId = clusterId,
        Host = host,
        DestinationAddress = destinationAddress,
        Path = path
    });

    return Results.Created($"/routes/{routeId}", new CreateRouteResponse(routeId, clusterId, host, destinationAddress, path));
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

public record CreateRouteRequest(string? RouteId, string? ClusterId, string? Host, string? DestinationAddress, string? Path);
public record CreateRouteResponse(string RouteId, string ClusterId, string Host, string DestinationAddress, string? Path);

public partial class Program { }
