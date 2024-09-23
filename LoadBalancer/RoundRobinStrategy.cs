namespace LoadBalancer;

public class RoundRobinStrategy(HealthCheckService healthCheckService) : ILoadBalancingStrategy
{
    private int _nextIndex;

    public async Task<string> GetBackendServer(HttpContext context, List<string> servers)
    {
        for (var i = 0; i < servers.Count; i++)
        {
            var index = Interlocked.Increment(ref _nextIndex) % servers.Count;
            var server = servers[index];

            if (await healthCheckService.IsServerHealthy(server))
                return server;
        }

        throw new Exception("No healthy backend servers available");
    }
}