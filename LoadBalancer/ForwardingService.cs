namespace LoadBalancer;

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
                if (string.IsNullOrEmpty(context.Request.Path))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;

                    await context.Response.WriteAsync("Invalid request path", cts.Token);

                    return;
                }

                var backendUrl = $"{backendServer}{context.Request.Path}{context.Request.QueryString}";

                var requestMessage = await CreateHttpRequestMessageAsync(context, backendUrl);

                var backendResponse = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                _logger.LogInformation("Received response {StatusCode} from {BackendServer}", backendResponse.StatusCode, backendServer);

                await CopyResponseFromBackend(context, backendResponse);

                break;
            }
            catch (TaskCanceledException) when (cts.Token.IsCancellationRequested)
            {
                _logger.LogWarning("Request to {BackendServer} timed out after {Timeout} ms", backendServer, _requestTimeout.TotalMilliseconds);

                context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;

                await context.Response.WriteAsync("Gateway Timeout", cts.Token);

                break;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error forwarding request to {BackendServer} on attempt {Attempt}", backendServer, attempt);

                if (attempt > _maxRetries)
                {
                    _logger.LogError("Exceeded max retries ({MaxRetries}) forwarding request to {BackendServer}", _maxRetries, backendServer);

                    context.Response.StatusCode = StatusCodes.Status502BadGateway;

                    await context.Response.WriteAsync("Bad Gateway", cts.Token);

                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
            }
        }

        _logger.LogInformation("Request forwarding completed in {RequestDuration} ms", (DateTime.UtcNow - requestStartTime).TotalMilliseconds);
    }

    private static async Task<HttpRequestMessage> CreateHttpRequestMessageAsync(HttpContext context, string backendUrl)
    {
        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(context.Request.Method),
            RequestUri = new Uri(backendUrl)
        };

        foreach (var header in context.Request.Headers)
        {
            if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        if (!(context.Request.ContentLength > 0) && !context.Request.Body.CanRead) 
            return requestMessage;

        context.Request.EnableBuffering();
        context.Request.Body.Position = 0;

        var memoryStream = new MemoryStream();
        await context.Request.Body.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        requestMessage.Content = new StreamContent(memoryStream);
        requestMessage.Content.Headers.ContentLength = memoryStream.Length;

        return requestMessage;
    }

    private static async Task CopyResponseFromBackend(HttpContext context, HttpResponseMessage backendResponse)
    {
        context.Response.StatusCode = (int)backendResponse.StatusCode;

        foreach (var header in backendResponse.Headers)
            if (header.Key != "Content-Length" && header.Key != "Transfer-Encoding")
                context.Response.Headers[header.Key] = header.Value.ToArray();

        foreach (var header in backendResponse.Content.Headers)
            if (header.Key != "Content-Length" && header.Key != "Transfer-Encoding")
                context.Response.Headers[header.Key] = header.Value.ToArray();

        await context.Response.StartAsync();

        await using var responseStream = await backendResponse.Content.ReadAsStreamAsync();
        await responseStream.CopyToAsync(context.Response.Body);

        await context.Response.CompleteAsync();
    }
}