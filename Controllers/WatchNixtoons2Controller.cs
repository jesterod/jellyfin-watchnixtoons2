using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Jellyfin.Plugin.WatchNixtoons2.Models;
using Jellyfin.Plugin.WatchNixtoons2.Services;

namespace Jellyfin.Plugin.WatchNixtoons2.Controllers;

[ApiController]
[Route("WatchNixtoons2/[action]")]
public class WatchNixtoons2Controller : ControllerBase
{
    private readonly WatchNixtoons2Service _service;
    private readonly HttpService _http;

    public WatchNixtoons2Controller(WatchNixtoons2Service service, HttpService http)
    {
        _service = service;
        _http = http;
    }

    // Temporary diagnostic: fetch a URL from inside Jellyfin with selectable TLS/HTTP options.

    [HttpGet]
    public async Task<ActionResult<List<CatalogSection>>> GetMainMenu()
    {
        var menu = await _service.GetMainMenuAsync();
        return Ok(menu);
    }
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36";

    private static readonly ConcurrentDictionary<string, (string VideoUrl, DateTime Expires)> ResolveCache = new();

    private static readonly HttpClient SharedHttpClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = DecompressionMethods.None,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    });

    [HttpGet]
    public IActionResult Index() => ServeBrowserPage();

    [HttpGet]
    public IActionResult Browser() => ServeBrowserPage();

    private IActionResult ServeBrowserPage()
    {
        var stream = typeof(WatchNixtoons2Controller).Assembly
            .GetManifestResourceStream("Jellyfin.Plugin.WatchNixtoons2.Configuration.Resources.browser.html");
        if (stream is null)
        {
            return NotFound();
        }

        using var reader = new StreamReader(stream);
        return Content(reader.ReadToEnd(), "text/html");
    }

    /// <summary>
    /// Streams the upstream video through Jellyfin. The upstream CDN rejects requests
    /// without a browser User-Agent, so the media source handed to the player points
    /// here instead of at the raw CDN URL.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task Stream(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var videoUrl = url;
        if (!videoUrl.Contains("/getvid", StringComparison.OrdinalIgnoreCase))
        {
            videoUrl = await ResolveVideoWithCacheAsync(url) ?? string.Empty;
        }

        if (string.IsNullOrEmpty(videoUrl))
        {
            Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, videoUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
        request.Headers.TryAddWithoutValidation("Referer", "https://embed.wcostream.com/");
        request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

        var range = Request.Headers["Range"].ToString();
        if (!string.IsNullOrEmpty(range))
        {
            request.Headers.TryAddWithoutValidation("Range", range);
        }

        using var response = await SharedHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        Response.StatusCode = (int)response.StatusCode;
        Response.Headers["Accept-Ranges"] = "bytes";
        if (response.Content.Headers.ContentLength is long length)
        {
            Response.ContentLength = length;
        }

        if (response.Content.Headers.ContentRange is not null)
        {
            Response.Headers["Content-Range"] = response.Content.Headers.ContentRange.ToString();
        }

        Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "video/mp4";

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await source.CopyToAsync(Response.Body, cancellationToken);
    }

    private async Task<string?> ResolveVideoWithCacheAsync(string pageUrl)
    {
        if (ResolveCache.TryGetValue(pageUrl, out var cached) && cached.Expires > DateTime.UtcNow)
        {
            return cached.VideoUrl;
        }

        var resolved = await _service.ResolveVideoUrlAsync(pageUrl);
        if (!string.IsNullOrEmpty(resolved))
        {
            ResolveCache[pageUrl] = (resolved, DateTime.UtcNow.AddMinutes(10));
        }

        return resolved;
    }
    [HttpGet]
    public async Task<ActionResult<List<CatalogItem>>> GetCatalog(string path, string? query = null, string? searchType = null)
    {
        var items = await _service.GetCatalogAsync(path, query, searchType);
        return Ok(items);
    }

    [HttpGet]
    public async Task<ActionResult<Dictionary<char, List<CatalogItem>>>> GetCatalogSections(string path, string? query = null, string? searchType = null)
    {
        var sections = await _service.GetCatalogSectionsAsync(path, query, searchType);
        return Ok(sections);
    }

    [HttpGet]
    public async Task<ActionResult<EpisodeListData>> GetEpisodes(string url)
    {
        var data = await _service.GetEpisodesAsync(url);
        return Ok(data);
    }

    [HttpGet]
    public async Task<ActionResult<PageMetadata>> GetMetadata(string url)
    {
        var metadata = await _service.GetPageMetadataAsync(url);
        return Ok(metadata);
    }

    [HttpGet]
    public async Task<ActionResult<List<CatalogItem>>> Search(string query, string searchType = "series")
    {
        var items = await _service.SearchAsync(query, searchType);
        return Ok(items);
    }

    [HttpGet]
    public async Task<ActionResult<List<CatalogItem>>> GetGenres()
    {
        var items = await _service.GetGenresAsync();
        return Ok(items);
    }

    [HttpGet]
    public async Task<ActionResult<string?>> ResolveVideo(string url, bool playChapters = false)
    {
        var videoUrl = await _service.ResolveVideoUrlAsync(url, playChapters);
        return Ok(videoUrl);
    }

    [HttpPost]
    public ActionResult AddToRecentlyWatched([FromBody] RecentlyWatchedRequest request)
    {
        _service.AddToRecentlyWatched(request.Name, request.Url);
        return Ok();
    }

    [HttpGet]
    public ActionResult<List<RecentlyWatchedItem>> GetRecentlyWatched()
    {
        var items = _service.GetRecentlyWatched();
        return Ok(items);
    }

    [HttpDelete]
    public ActionResult RemoveFromRecentlyWatched(string url)
    {
        var removed = _service.RemoveFromRecentlyWatched(url);
        return Ok(removed);
    }
}

public class RecentlyWatchedRequest
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}