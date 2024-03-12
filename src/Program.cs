var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .ConfigureHttpClient((c, h) => {
        h.AllowAutoRedirect = true;
         
    })
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
var app = builder.Build();

app.MapReverseProxy();

app.Run();