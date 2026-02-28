using Serilog;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace AppPilot.Services.HealthCheck;

public interface IHealthChecker
{
    // Returns null when healthy; returns an error description when unhealthy.
    Task<string?> CheckHealthAsync(string url);
}

public class HttpHealthChecker : IHealthChecker
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public HttpHealthChecker(ILogger logger)
    {
        _logger = logger;

        // Bypass SSL certificate validation — this tool targets local dev where
        // self-signed / dev certificates are common.
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5),
            // Try HTTP/2 first (required for gRPC), fall back to HTTP/1.1 for plain APIs.
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
    }

    public async Task<string?> CheckHealthAsync(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        try
        {
            var response = await _httpClient.GetAsync(url);
            var code = (int)response.StatusCode;

            // 5xx = server error; 1xx–4xx = service is responding (even if route is missing)
            if (code >= 500)
                return $"HTTP {code} {response.ReasonPhrase}";

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.Debug(ex, "Health check connection failed for {Url}", url);
            return $"Connection failed: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            _logger.Debug("Health check timed out for {Url}", url);
            return "Health check timed out after 5 seconds";
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Health check error for {Url}", url);
            return ex.Message;
        }
    }
}
