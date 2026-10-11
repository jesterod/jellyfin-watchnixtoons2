using Jellyfin.Plugin.WatchNixtoons2.Configuration;
using Jellyfin.Plugin.WatchNixtoons2.Models;
using Jellyfin.Plugin.WatchNixtoons2.Scrapers;

namespace Jellyfin.Plugin.WatchNixtoons2.Services;

public class WatchNixtoons2Service
{
    private readonly HttpService _httpService;
    private readonly PluginConfiguration _config;
    private readonly BaseScraper _scraper;
    private readonly CatalogService _catalogService;
    private readonly VideoResolver _videoResolver;
    private readonly RecentlyWatchedService _recentlyWatchedService;

    public WatchNixtoons2Service(PluginConfiguration config)
    {
        _config = config;
        _httpService = new HttpService(config);
        
        var siteType = Enum.TryParse<SiteType>(_config.BaseUrl, out var parsed) 
            ? parsed 
            : ScraperFactory.GetDefaultSiteType();
        
        _scraper = ScraperFactory.CreateScraper(siteType, _httpService);
        _catalogService = new CatalogService(_httpService, config, _scraper);
        _videoResolver = new VideoResolver(_scraper);
        _recentlyWatchedService = new RecentlyWatchedService(config);
    }

    public async Task<List<CatalogSection>> GetMainMenuAsync()
    {
        return await _catalogService.GetMainMenuAsync();
    }

    public async Task<List<CatalogItem>> GetCatalogAsync(string path, string? query = null, string? searchType = null)
    {
        return await _catalogService.GetCatalogAsync(path, query, searchType);
    }

    public async Task<Dictionary<char, List<CatalogItem>>> GetCatalogSectionsAsync(string path, string? query = null, string? searchType = null)
    {
        return await _catalogService.GetCatalogSectionsAsync(path, query, searchType);
    }

    public async Task<EpisodeListData> GetEpisodesAsync(string url)
    {
        var data = await _catalogService.GetEpisodesAsync(url);
        
        // Add to recently watched
        // We need the show title - for now we'll skip this and let the caller handle it
        
        return data;
    }

    public async Task<PageMetadata> GetPageMetadataAsync(string url)
    {
        return await _catalogService.GetPageMetadataAsync(url);
    }

    public async Task<List<CatalogItem>> SearchAsync(string query, string searchType)
    {
        return await _catalogService.SearchAsync(query, searchType);
    }

    public async Task<List<CatalogItem>> GetGenresAsync()
    {
        return await _catalogService.GetGenresAsync();
    }

    public async Task<string?> ResolveVideoUrlAsync(string url, bool playChapters = false)
    {
        return await _videoResolver.ResolveAsync(url, playChapters);
    }

    public void AddToRecentlyWatched(string name, string url)
    {
        _recentlyWatchedService.Add(name, url);
    }

    public IReadOnlyList<RecentlyWatchedItem> GetRecentlyWatched()
    {
        return _recentlyWatchedService.GetAll();
    }

    public bool RemoveFromRecentlyWatched(string url)
    {
        return _recentlyWatchedService.Remove(url);
    }

    public void ClearCache()
    {
        _catalogService.ClearCache();
    }

    public void Dispose()
    {
        _httpService.Dispose();
    }
}