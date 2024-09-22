namespace LoadBalancer
{
    public interface ILoadBalancingStrategy
    {
        Task<string> GetBackendServer(HttpContext context, List<string> backendServers);
    }
}