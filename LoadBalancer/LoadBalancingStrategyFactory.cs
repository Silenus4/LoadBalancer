namespace LoadBalancer;

public class LoadBalancingStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;

    public LoadBalancingStrategyFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ILoadBalancingStrategy GetStrategy(string strategy)
    {
        return strategy switch
        {
            "RoundRobin" => _serviceProvider.GetRequiredService<RoundRobinStrategy>(),
            "StickySession" => _serviceProvider.GetRequiredService<StickySessionStrategy>(),
            _ => throw new NotSupportedException($"Unknown strategy: {strategy}")
        };
    }
}