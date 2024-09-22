using LoadBalancer;

public class RoundRobinStrategy : ILoadBalancingStrategy
{
    private int _nextIndex = 0;
    private readonly HealthCheckService _healthCheckService;

    public RoundRobinStrategy(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    public async Task<string> GetBackendServer(HttpContext context, List<string> servers)
    {
        for (int i = 0; i < servers.Count; i++)
        {
            var index = Interlocked.Increment(ref _nextIndex) % servers.Count;
            var server = servers[index];

            if (await _healthCheckService.IsServerHealthy(server))
            {
                return server;
            }
        }

        throw new Exception("No healthy backend servers available");
    }
}