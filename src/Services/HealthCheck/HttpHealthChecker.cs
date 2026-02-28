using Serilog;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace AppPilot.Services.HealthCheck;

public interface IHealthChecker
{
    Task<bool> CheckHealthAsync(string url);
}

public class HttpHealthChecker : IHealthChecker
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public HttpHealthChecker(ILogger logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public async Task<bool> CheckHealthAsync(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        try
        {
            var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Health check failed for {Url}", url);
            return false;
        }
    }
}
