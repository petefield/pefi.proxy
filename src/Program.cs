using Microsoft.Extensions.Options;
using MongoDB.Driver;
using pefi;
using pefi.dynamicdns.Services;
using PeFi.Proxy;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
builder.Logging.AddConsole();
builder.Services.AddHttpClient<ServiceManagerClient>((sp,c) => {
    var baseAddress = builder.Configuration.GetSection("ServiceManager").GetValue<string>("baseurl") ?? "";
    c.BaseAddress = new Uri(baseAddress); 
});




builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHostedService<ProxyConfig>();
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

app.MapGet("/config", (InMemoryConfigProvider memoryConfigProvider, IProxyConfigProvider  appSettingsConfigProvider ) => {
    var appSettingsConfig = appSettingsConfigProvider.GetConfig();
    var memoryConfig = memoryConfigProvider.GetConfig();

    return new{
        Routes =  appSettingsConfig.Routes.Concat(memoryConfig.Routes),
        Clusters = appSettingsConfig.Clusters.Concat(memoryConfig.Clusters),
    };
}).WithName("Get Current Config")
.WithOpenApi();

app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.MapReverseProxy();
app.Run();

