using MongoDB.Bson;
using pefi.Rabbit;
using PeFi.Proxy.Persistance;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IDataStore, MongoDatastore>();
builder.Services.AddHostedService<ProxyConfig>();
builder.Services.AddControllers();
builder.Services.AddReverseProxy().LoadFromMemory([], []);
builder.Services.AddSingleton<IMessageBroker>(sp => new MessageBroker("192.168.0.5", "username", "password"));

var app = builder.Build();

app.MapReverseProxy();
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/config", (InMemoryConfigProvider configProvider) => {
    var proxyConfig = configProvider.GetConfig();
    return proxyConfig;
})
.WithName("Get Current Config")
.WithOpenApi();

app.Run();

