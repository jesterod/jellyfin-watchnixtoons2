using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.WatchNixtoons2.Configuration;

namespace Jellyfin.Plugin.WatchNixtoons2.Services;

public class HttpService : IDisposable
{
    public const string BuildTag = "1.0.10.0-tls-fix";

    private readonly HttpClient _httpClient;
    private readonly PluginConfiguration _config;
    private readonly Dictionary<string, DateTime> _lastRequestTimes = new();
    private readonly object _lock = new();

    public HttpService(PluginConfiguration config)
    {
        _config = config;
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                // Pin a single TLS version. With the default (None) - or with a multi-version
                // set such as Tls12|Tls13 - the .NET/OpenSSL ClientHello is scored by
                // Cloudflare's bot management and answered with a managed challenge
                // ("Just a moment..."). Pinning exactly one version produces a normal
                // ClientHello that is allowed through. TLS 1.2 is used because every host
                // this plugin talks to supports it.
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12,
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            },
            // Prefer IPv4. Some hosting/Docker egress paths only have a flagged or
            // challenged IPv6 route, which Cloudflare answers with a managed challenge.
            ConnectCallback = async (context, cancellationToken) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);
                var target =
                    Array.Find(addresses, a => a.AddressFamily == AddressFamily.InterNetwork)
                    ?? (addresses.Length > 0 ? addresses[0] : null);

                if (target is null)
                    throw new HttpRequestException($"Could not resolve {context.DnsEndPoint.Host}");

                var socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(target, context.DnsEndPoint.Port), cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36");
    }

    private static void ApplyBrowserHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
        request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "none");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        request.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"151\", \"Not:A-Brand\";v=\"24\", \"Google Chrome\";v=\"151\"");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
    }

    private async Task<string> SendAsync(HttpRequestMessage request, string url)
    {
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"[build {BuildTag}] HTTP {(int)response.StatusCode} for {url}; " +
                $"UA=[{request.Headers.UserAgent}] " +
                $"cf-mitigated=[{(response.Headers.TryGetValues("cf-mitigated", out var cf) ? string.Join(",", cf) : "-")}] " +
                $"body=[{(body.Length > 400 ? body[..400] : body).Replace('\n', ' ').Replace('\r', ' ')}]");
        }

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetAsync(string url, Dictionary<string, string>? extraHeaders = null)
    {
        EnforceRateLimit(new Uri(url).Host);
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyBrowserHeaders(request);

        if (extraHeaders != null)
        {
            foreach (var header in extraHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var content = await SendAsync(request, url);
        UpdateLastRequestTime(new Uri(url).Host);
        return content;
    }

    public async Task<string> PostAsync(string url, Dictionary<string, string> formData, Dictionary<string, string>? extraHeaders = null)
    {
        EnforceRateLimit(new Uri(url).Host);
        
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        ApplyBrowserHeaders(request);
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        request.Headers.TryAddWithoutValidation("Origin", new Uri(url).GetLeftPart(UriPartial.Authority));

        if (extraHeaders != null)
        {
            foreach (var header in extraHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        request.Content = new FormUrlEncodedContent(formData);
        var content = await SendAsync(request, url);
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