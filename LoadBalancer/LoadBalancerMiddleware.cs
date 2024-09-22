using LoadBalancer;
using Microsoft.Extensions.Options;

public class LoadBalancerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly LoadBalancingStrategyFactory _strategyFactory;
    private readonly List<string> _backendServers;
    private readonly string _selectedStrategy;
    private readonly ForwardingService _forwardingService;

    public LoadBalancerMiddleware(
        RequestDelegate next,
        LoadBalancingStrategyFactory strategyFactory,
        IOptions<LoadBalancerOptions> options)
    {
        _next = next;
        _strategyFactory = strategyFactory;
        _backendServers = options.Value.BackendServers;
        _selectedStrategy = options.Value.Strategy;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var strategy = _strategyFactory.GetStrategy(_selectedStrategy);
        var backendServer = await strategy.GetBackendServer(context, _backendServers);

        // Forward the request to the selected backend server
        await _forwardingService.ForwardRequestToBackend(context, backendServer);
    }
}