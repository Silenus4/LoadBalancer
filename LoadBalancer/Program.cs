using LoadBalancer;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddSingleton<HealthCheckService>();

builder.Services.AddSingleton(serviceProvider => new ForwardingService(
    serviceProvider.GetRequiredService<HttpClient>(),
    serviceProvider.GetRequiredService<ILogger<ForwardingService>>(),
    TimeSpan.FromSeconds(30),
    3
));

builder.Services.AddSingleton<RoundRobinStrategy>();
builder.Services.AddSingleton<StickySessionStrategy>();
builder.Services.AddSingleton<LoadBalancingStrategyFactory>();

builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect("localhost"));

builder.Services.Configure<LoadBalancerOptions>(builder.Configuration.GetSection("LoadBalancer"));

var app = builder.Build();

app.UseMiddleware<LoadBalancerMiddleware>();

app.Run();