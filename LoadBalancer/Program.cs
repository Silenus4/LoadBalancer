using LoadBalancer;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Register HttpClient with a timeout and retry strategy
builder.Services.AddHttpClient(); // Ensures HttpClient is injected where needed

// Register ForwardingService with required dependencies and configurations
builder.Services.AddSingleton(serviceProvider => new ForwardingService(
    serviceProvider.GetRequiredService<HttpClient>(),
    serviceProvider.GetRequiredService<ILogger<ForwardingService>>(),
    TimeSpan.FromSeconds(30), // Custom request timeout for backend requests
    3 // Maximum retries on failure
));

// Register strategies, strategy factory, and other services
builder.Services.AddSingleton<RoundRobinStrategy>();
builder.Services.AddSingleton<StickySessionStrategy>();
builder.Services.AddSingleton<LoadBalancingStrategyFactory>();

// Register Redis connection for StickySessionStrategy
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("localhost"));

// Register LoadBalancerOptions from appsettings.json
builder.Services.Configure<LoadBalancerOptions>(builder.Configuration.GetSection("LoadBalancer"));

var app = builder.Build();