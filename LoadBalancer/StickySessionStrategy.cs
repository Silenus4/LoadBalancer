using StackExchange.Redis;

namespace LoadBalancer;

public class StickySessionStrategy(IConnectionMultiplexer redis, HealthCheckService healthCheckService)
    : ILoadBalancingStrategy
{
    private readonly IDatabase _db = redis.GetDatabase();
    private int _nextIndex;

    public async Task<string> GetBackendServer(HttpContext context, List<string> backendServers)
    {
        var sessionId = context.Request.Cookies["session-id"];

        if (sessionId != null)
        {
            var backendServer = await _db.StringGetAsync(sessionId);

            if (backendServer.HasValue && await healthCheckService.IsServerHealthy(backendServer))
            {
                return backendServer;
            }
        }

        //TODO: ???
        return await GetHealthyRoundRobinServer(backendServers);
    }

    private async Task<string> GetHealthyRoundRobinServer(IReadOnlyList<string> backendServers)
    {
        foreach (var item in backendServers)
        {
            var index = Interlocked.Increment(ref _nextIndex) % backendServers.Count;
            var server = backendServers[index];

            if (await healthCheckService.IsServerHealthy(server))
            {
                return server;
            }
        }

        throw new Exception("No healthy backend servers available");
    }
}