using MongoDB.Bson;
using pefi.Rabbit;
using PeFi.Proxy;
using PeFi.Proxy.Persistance;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<IDataStore, MongoDatastore>();
builder.Services.AddHostedService<ProxyConfig>();
builder.Services.AddSwaggerGen();
builder.Services.AddReverseProxy()
    .LoadFromMemory([], [])
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddSingleton<IMessageBroker>(sp => new MessageBroker("192.168.0.5", "username", "password"));

var app = builder.Build();

app.MapGet("/config", (InMemoryConfigProvider memoryConfigProvider, IProxyConfigProvider  appSettingsConfigProvider ) => {
    var appSettingsConfig = appSettingsConfigProvider.GetConfig();
    var memoryConfig = appSettingsConfigProvider.GetConfig();

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

