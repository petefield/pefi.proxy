using pefi;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
builder.Logging.AddConsole();

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

var app = builder.Build();

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

app.UseBlazorFrameworkFiles("/dashboard");
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.MapReverseProxy();

app.MapFallbackToFile("/dashboard/{**path:nonfile}", "dashboard/index.html");
app.MapFallbackToFile("/dashboard", "dashboard/index.html");
app.Run();

public partial class Program { }
