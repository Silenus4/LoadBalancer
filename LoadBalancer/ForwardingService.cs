public class ForwardingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ForwardingService> _logger;
    private readonly TimeSpan _requestTimeout;
    private readonly int _maxRetries;

    public ForwardingService(HttpClient httpClient, ILogger<ForwardingService> logger, TimeSpan requestTimeout, int maxRetries)
    {
        _httpClient = httpClient;
        _logger = logger;
        _requestTimeout = requestTimeout;
        _maxRetries = maxRetries;
    }

    public async Task ForwardRequestToBackend(HttpContext context, string backendServer)
    {
        // Start logging the process
        _logger.LogInformation("Starting to forward request to {BackendServer}", backendServer);
        var requestStartTime = DateTime.UtcNow;

        using var cts = new CancellationTokenSource(_requestTimeout);
        context.RequestAborted.Register(cts.Cancel);

        var attempt = 0;

        while (attempt <= _maxRetries)
        {
            attempt++;
            try
            {
                // Forward the request to the backend server
                var requestMessage = CreateHttpRequestMessage(context, backendServer);
                var backendResponse = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                // Log the backend response
                _logger.LogInformation("Received response {StatusCode} from {BackendServer}", backendResponse.StatusCode, backendServer);

                // Forward the response back to the client
                await CopyResponseFromBackend(context, backendResponse);
                break; // Success, exit the retry loop
            }
            catch (TaskCanceledException) when (cts.Token.IsCancellationRequested)
            {
                // Timeout or client disconnected
                _logger.LogWarning("Request to {BackendServer} timed out after {Timeout} ms", backendServer, _requestTimeout.TotalMilliseconds);
                context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
                await context.Response.WriteAsync("Gateway Timeout");
                break; // Exit on timeout
            }
            catch (HttpRequestException ex)
            {
                // Log any request exceptions (network failures, DNS failures, etc.)
                _logger.LogError(ex, "Error forwarding request to {BackendServer} on attempt {Attempt}", backendServer, attempt);

                if (attempt > _maxRetries)
                {
                    _logger.LogError("Exceeded max retries ({MaxRetries}) forwarding request to {BackendServer}", _maxRetries, backendServer);
                    context.Response.StatusCode = StatusCodes.Status502BadGateway;
                    await context.Response.WriteAsync("Bad Gateway");
                    break;
                }

                // Retry on next iteration
                await Task.Delay(TimeSpan.FromSeconds(2)); // Add a small delay before retrying
            }
        }

        // Log the total request duration
        var requestDuration = DateTime.UtcNow - requestStartTime;
        _logger.LogInformation("Request forwarding completed in {RequestDuration} ms", requestDuration.TotalMilliseconds);
    }

    private static HttpRequestMessage CreateHttpRequestMessage(HttpContext context, string backendServer)
    {
        var requestMessage = new HttpRequestMessage
        {
            // Set method and request URI
            Method = new HttpMethod(context.Request.Method),
            RequestUri = new Uri($"{backendServer}{context.Request.Path}{context.Request.QueryString}")
        };

        // Copy the request headers
        foreach (var header in context.Request.Headers)
        {
            if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        // Copy the body if present
        if (context.Request.ContentLength > 0 || context.Request.Body.CanRead)
        {
            requestMessage.Content = new StreamContent(context.Request.Body);
        }

        return requestMessage;
    }

    private static async Task CopyResponseFromBackend(HttpContext context, HttpResponseMessage backendResponse)
    {
        // Copy status code
        context.Response.StatusCode = (int)backendResponse.StatusCode;

        // Copy headers from the backend response
        foreach (var header in backendResponse.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in backendResponse.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        // Ensure headers are flushed to the client
        await context.Response.StartAsync();

        // Copy body
        using var responseStream = await backendResponse.Content.ReadAsStreamAsync();
        await responseStream.CopyToAsync(context.Response.Body);
    }
}