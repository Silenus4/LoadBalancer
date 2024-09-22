namespace LoadBalancer
{
    public class LoadBalancerOptions
    {
        public string Strategy { get; set; }

        public List<string> BackendServers { get; set; }
    }
}