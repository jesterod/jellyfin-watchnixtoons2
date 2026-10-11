using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Jellyfin.Plugin.WatchNixtoons2.Models;
using Jellyfin.Plugin.WatchNixtoons2.Services;

namespace Jellyfin.Plugin.WatchNixtoons2.Controllers;

[ApiController]
[Route("WatchNixtoons2/[action]")]
public class WatchNixtoons2Controller : ControllerBase
{
    private readonly WatchNixtoons2Service _service;

    public WatchNixtoons2Controller(WatchNixtoons2Service service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<CatalogSection>>> GetMainMenu()
    {
        var menu = await _service.GetMainMenuAsync();
        return Ok(menu);
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