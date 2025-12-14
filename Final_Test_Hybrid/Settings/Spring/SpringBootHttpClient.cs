using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Settings.Spring;

public class SpringBootHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpringBootHttpClient> _logger;

    public SpringBootHttpClient(IOptions<SpringBootSettings> options, ILogger<SpringBootHttpClient> logger)
    {
        var settings = options.Value;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(settings.BaseUrl),
            Timeout = TimeSpan.FromMilliseconds(settings.TimeoutMs)
        };
    }

    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Endpoint} failed", endpoint);
            throw;
        }
    }

    public async Task<string> GetStringAsync(string endpoint, CancellationToken ct = default)
    {
        try
        {
            return await _httpClient.GetStringAsync(endpoint, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Endpoint} failed", endpoint);
            throw;
        }
    }

    public async Task<bool> IsReachableAsync(string endpoint, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, data, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TResponse>(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Endpoint} failed", endpoint);
            throw;
        }
    }

    public async Task PostAsync<TRequest>(string endpoint, TRequest data, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, data, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Endpoint} failed", endpoint);
            throw;
        }
    }

    public async Task<HttpResponseMessage> PostWithResponseAsync<TRequest>(string endpoint, TRequest data, CancellationToken ct = default)
    {
        return await _httpClient.PostAsJsonAsync(endpoint, data, ct).ConfigureAwait(false);
    }
}
