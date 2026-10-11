using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.WatchNixtoons2.Configuration;
using Jellyfin.Plugin.WatchNixtoons2.Models;
using Jellyfin.Plugin.WatchNixtoons2.Scrapers;
using Jellyfin.Plugin.WatchNixtoons2.Services;

namespace Jellyfin.Plugin.WatchNixtoons2.Services;

public class CatalogService
{
    private readonly HttpService _httpService;
    private readonly PluginConfiguration _config;
    private readonly BaseScraper _scraper;
    private readonly ConcurrentDictionary<string, List<CatalogItem>> _catalogCache = new();
    private readonly ConcurrentDictionary<string, EpisodeListData> _episodeCache = new();
    private readonly ConcurrentDictionary<string, PageMetadata> _metadataCache = new();

    private static readonly Dictionary<string, string> UrlPaths = new()
    {
        { "latest", "latest" },
        { "popular", "popular" },
        { "dubbed", "/dubbed-anime-list" },
        { "cartoons", "/cartoon-list" },
        { "subbed", "/subbed-anime-list" },
        { "movies", "/movie-list" },
        { "latestmovies", "/anime/movies" },
        { "ova", "/ova-list" },
        { "search", "/search" },
        { "genre", "/search-by-genre" }
    };

    public CatalogService(HttpService httpService, PluginConfiguration config, BaseScraper scraper)
    {
        _httpService = httpService;
        _config = config;
        _scraper = scraper;
    }

    public async Task<List<CatalogSection>> GetMainMenuAsync()
    {
        var sections = new List<CatalogSection>
        {
            new CatalogSection { Name = "Latest Releases", Items = await GetCatalogAsync("latest") },
            new CatalogSection { Name = "Latest Movies", Items = await GetCatalogAsync("latestmovies") },
            new CatalogSection { Name = "Popular & Ongoing", Items = await GetCatalogAsync("popular") },
            new CatalogSection { Name = "Dubbed Anime", Items = await GetCatalogAsync("dubbed") },
            new CatalogSection { Name = "Cartoons", Items = await GetCatalogAsync("cartoons") },
            new CatalogSection { Name = "Subbed Anime", Items = await GetCatalogAsync("subbed") },
            new CatalogSection { Name = "Movies", Items = await GetCatalogAsync("movies") },
            new CatalogSection { Name = "OVAs", Items = await GetCatalogAsync("ova") }
        };

        return sections;
    }

    public async Task<List<CatalogItem>> GetCatalogAsync(string path, string? query = null, string? searchType = null)
    {
        var cacheKey = $"{path}:{query}:{searchType}";
        
        if (_catalogCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var items = await _scraper.GetCatalogAsync(path, query, searchType);
        
        // Apply catalog filtering based on settings
        if (!_config.ShowCatalog && items.Count > 1)
        {
            // If show catalog is disabled, we could flatten but let's keep sections
        }

        _catalogCache[cacheKey] = items;
        return items;
    }

    public async Task<Dictionary<char, List<CatalogItem>>> GetCatalogSectionsAsync(string path, string? query = null, string? searchType = null)
    {
        var items = await GetCatalogAsync(path, query, searchType);
        var sections = new Dictionary<char, List<CatalogItem>>();

        foreach (var item in items)
        {
            var firstChar = char.ToUpper(item.Title.FirstOrDefault(c => char.IsLetterOrDigit(c)));
            var key = char.IsLetterOrDigit(firstChar) ? firstChar : '#';
            
            if (!sections.ContainsKey(key))
                sections[key] = new List<CatalogItem>();
            
            sections[key].Add(item);
        }

        return sections;
    }

    public async Task<EpisodeListData> GetEpisodesAsync(string url)
    {
        if (_episodeCache.TryGetValue(url, out var cached))
            return cached;

        var data = await _scraper.GetEpisodesAsync(url);
        _episodeCache[url] = data;
        return data;
    }

    public async Task<PageMetadata> GetPageMetadataAsync(string url)
    {
        if (_metadataCache.TryGetValue(url, out var cached))
            return cached;

        var data = await _scraper.GetPageMetadataAsync(url);
        _metadataCache[url] = data;
        return data;
    }

    public async Task<List<CatalogItem>> SearchAsync(string query, string searchType)
    {
        return await GetCatalogAsync("search", query, searchType);
    }

    public async Task<List<CatalogItem>> GetGenresAsync()
    {
        return await GetCatalogAsync("genre");
    }

    public void ClearCache()
    {
        _catalogCache.Clear();
        _episodeCache.Clear();
        _metadataCache.Clear();
    }

    public void InvalidateCache(string path)
    {
        var keysToRemove = _catalogCache.Keys.Where(k => k.StartsWith(path)).ToList();
        foreach (var key in keysToRemove)
        {
            _catalogCache.TryRemove(key, out _);
        }
    }
}