using pefi.dynamicdns.Services;
using pefi.Rabbit;
using PeFi.Proxy;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<ServiceManagerClient>(c => c.BaseAddress = new Uri("http://192.168.0.5:5550"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHostedService<ProxyConfig>();
builder.Services.AddSwaggerGen();
builder.Services.AddReverseProxy()
    .LoadFromMemory([], [])
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddSingleton<IMessageBroker>(sp => new MessageBroker("192.168.0.5", "username", "password"));

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

