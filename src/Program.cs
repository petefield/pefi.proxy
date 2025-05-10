using pefi.Rabbit;
using PeFi.Proxy.Persistance;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDataStore, MongoDatastore>();
builder.Services.AddHostedService<ProxyConfig>();
builder.Services.AddControllers();
builder.Services.AddReverseProxy().LoadFromMemory([], []);
builder.Services.AddSingleton<IMessageBroker>(sp => new MessageBroker("192.168.0.5", "username", "password"));

var app = builder.Build();
app.MapReverseProxy();
app.Run();

