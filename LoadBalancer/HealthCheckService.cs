namespace LoadBalancer;

public class HealthCheckService
{
    private readonly HttpClient _httpClient;

    public HealthCheckService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> IsServerHealthy(string serverUrl)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{serverUrl}/health");

            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}