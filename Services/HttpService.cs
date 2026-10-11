using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.WatchNixtoons2.Configuration;

namespace Jellyfin.Plugin.WatchNixtoons2.Services;

public class HttpService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly PluginConfiguration _config;
    private readonly Dictionary<string, DateTime> _lastRequestTimes = new();
    private readonly object _lock = new();

    public HttpService(PluginConfiguration config)
    {
        _config = config;
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36");
    }

    public async Task<string> GetAsync(string url, Dictionary<string, string>? extraHeaders = null)
    {
        EnforceRateLimit(new Uri(url).Host);
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.5");
        request.Headers.Add("Cache-Control", "no-cache");

        if (extraHeaders != null)
        {
            foreach (var header in extraHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        UpdateLastRequestTime(new Uri(url).Host);
        return content;
    }

    public async Task<string> PostAsync(string url, Dictionary<string, string> formData, Dictionary<string, string>? extraHeaders = null)
    {
        EnforceRateLimit(new Uri(url).Host);
        
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.5");
        request.Headers.Add("Cache-Control", "no-cache");

        if (extraHeaders != null)
        {
            foreach (var header in extraHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        request.Content = new FormUrlEncodedContent(formData);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        UpdateLastRequestTime(new Uri(url).Host);
        return content;
    }

    private void EnforceRateLimit(string host)
    {
        lock (_lock)
        {
            if (_lastRequestTimes.TryGetValue(host, out var lastTime))
            {
                var elapsed = DateTime.Now - lastTime;
                if (elapsed.TotalMilliseconds < _config.RequestDelay)
                {
                    var delay = _config.RequestDelay - (int)elapsed.TotalMilliseconds;
                    Task.Delay(delay).Wait();
                }
            }
        }
    }

    private void UpdateLastRequestTime(string host)
    {
        lock (_lock)
        {
            _lastRequestTimes[host] = DateTime.Now;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}