using Microsoft.Extensions.Options;

namespace LoadBalancer;

public class LoadBalancerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILoadBalancingStrategy _strategy;
    private readonly List<string> _backendServers;
    private readonly ForwardingService _forwardingService;

    public LoadBalancerMiddleware(
        RequestDelegate next,
        LoadBalancingStrategyFactory strategyFactory,
        ForwardingService forwardingService,
        IOptions<LoadBalancerOptions> options)
    {
        _next = next;
        _backendServers = options.Value.BackendServers;
        _forwardingService = forwardingService;
        _strategy = strategyFactory.GetStrategy(options.Value.Strategy);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var backendServer = await _strategy.GetBackendServer(context, _backendServers);

        await _forwardingService.ForwardRequestToBackend(context, backendServer);

        await _next(context);
    }
}