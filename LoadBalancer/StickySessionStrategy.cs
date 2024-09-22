using LoadBalancer;
using StackExchange.Redis;

public class StickySessionStrategy : ILoadBalancingStrategy
{
    private readonly IDatabase _db;
    private readonly HealthCheckService _healthCheckService;
    private int _nextIndex = 0;

    public StickySessionStrategy(IConnectionMultiplexer redis, HealthCheckService healthCheckService)
    {
        _db = redis.GetDatabase();
        _healthCheckService = healthCheckService;
    }

    public async Task<string> GetBackendServer(HttpContext context, List<string> backendServers)
    {
        var sessionId = context.Request.Cookies["session-id"];

        if (sessionId != null)
        {
            var backendServer = await _db.StringGetAsync(sessionId);

            if (backendServer.HasValue && await _healthCheckService.IsServerHealthy(backendServer))
            {
                return backendServer;
            }
        }

        return await GetHealthyRoundRobinServer(backendServers);
    }

    private async Task<string> GetHealthyRoundRobinServer(List<string> backendServers)
    {
        for (int i = 0; i < backendServers.Count; i++)
        {
            var index = Interlocked.Increment(ref _nextIndex) % backendServers.Count;
            var server = backendServers[index];

            if (await _healthCheckService.IsServerHealthy(server))
            {
                return server;
            }
        }

        throw new Exception("No healthy backend servers available");
    }
}
