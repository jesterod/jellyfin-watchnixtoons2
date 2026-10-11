using System.Text.RegularExpressions;
using Jellyfin.Plugin.WatchNixtoons2.Models;
using Jellyfin.Plugin.WatchNixtoons2.Services;

namespace Jellyfin.Plugin.WatchNixtoons2.Scrapers;

public class WcoforeverScraper : BaseScraper
{
    private readonly HttpService _httpService;

    public WcoforeverScraper(HttpService httpService, string baseUrl) 
        : base(CreateSettings(), baseUrl, httpService)
    {
        _httpService = httpService;
    }

    private static SiteSettings CreateSettings()
    {
        return new SiteSettings
        {
            Catalog = new ScrapeRule
            {
                Regex = @"<li(?:\sdata\-id=""[0-9]+""?)?>\s*<a href=""([^""]+).*?>([^<]+)",
                Start = @"""ddmcc""",
                StartAlt = null,
                End = @"<script>"
            },
            Episode = new ScrapeRule
            {
                Regex = @"<a href=""(?<link>[^""]+).*?(data-lang=""(?<type>[^""]+)"")?>(?:<span>)?(?<name>[^<]+)",
                Start = @"name=""pid""",
                End = @"""sidebar-all"""
            },
            EpisodesPage = new ScrapeRule
            {
                // Current wcoforever series pages list episodes in <div id="episodeList">
                // as <a href="..." class="dark-episode-item"><span>Title</span>.
                Regex = @"href=""(?<link>[^""]+)""\s*[^>]*>\s*<span>(?<name>[^<]+)",
                Start = @"id=""episodeList""",
                End = @"class=""result-meta"""
            },
            SeriesSearch = new ScrapeRule
            {
                // Search results render each hit as <a href="..." rel="bookmark">Title</a>.
                Regex = @"<a\s+href=""(?<link>[^""]+)""\s+rel=""bookmark"">(?<name>[^<]+)",
                Start = @"id=""konuara""",
                End = @"<script>"
            },
            MovieSearch = new ScrapeRule
            {
                Regex = @"<a href=""(?<link>[^""]+).*?>(?<name>[^<]+)",
                Start = @"""ddmcc""",
                End = @"/ul></ul"
            },
            EpisodeSearch = new ScrapeRule
            {
                Regex = @"<a href=""(?<link>[^""]+)[^>]*>(?<name>[^<]+)</a",
                Start = @"""submit""",
                End = @"""cizgiyazisi"
            },
            Genre = new ScrapeRule
            {
                Regex = @"href=""([^""]+)""\s*class=""genre-buton"">\s*<span(?:[^>]+)>(.*?)</",
                Start = @"sidebar_cat",
                End = @"class=""recent-release-main"""
            },
            Thumbnail = new ScrapeRule
            {
                Regex = string.Empty,
                Start = @"og:image"" content=""",
                End = string.Empty
            },
            PageMeta = new ScrapeRule
            {
                Regex = @"href=""([^""]+)""",
                Start = @"""header-tag""",
                End = string.Empty
            },
            PagePlot = new ScrapeRule
            {
                Regex = @"</h3>\s*<p>(.*?)</p>",
                Start = @"Info:",
                End = string.Empty
            },
            Latest = new LatestSettings
            {
                Url = "/last-50-recent-release",
                Regex = @"src=""(?<img>[^""]+)"">\s*</a>\s*</div>\s*<div\s*class=""recent-release-episodes""><a\s*href=""(?<link>[^""]+)""\s*rel=""bookmark"">(?<name>.*?)</a",
                Start = @"class=""recent-release-main""",
                End = @"</body>"
            },
            LatestMovies = new ScrapeRule
            {
                Regex = @"href=""([^""]+)""\s*[^>]*>\s*<span>([^<]+)",
                Start = @"id=""episodeList""",
                End = @"class=""result-meta"""
            },
            Popular = new ScrapeRule
            {
                Regex = @"<a href=""([^""]+).*?>([^<]+)",
                Start = @"""sidebar-titles""",
                End = @"</div>"
            },
            Parent = new ScrapeRule
            {
                Regex = @"<h2><a href=""([^""]+)""(?:[^>]+)?><span(?:[^>]+)>([^/<]+)<",
                Start = @"""header-tag""",
                End = string.Empty
            },
            Chapter = new ScrapeRule
            {
                Regex = @"onclick=""myFunction.*<script",
                Start = string.Empty,
                End = string.Empty
            },
            DecodeSourceRequired = true
        };
    }

    public override async Task<List<CatalogItem>> GetCatalogAsync(string path, string? query = null, string? searchType = null)
    {
        // Search on this site is a POST form (catara + konuara), not a query string.
        if (path == "search")
        {
            var konuara = searchType == "episodes" ? "episodes" : "series";
            var searchRule = searchType == "episodes" ? Settings.EpisodeSearch : Settings.SeriesSearch;
            return await PostCatalogAsync(
                BaseUrl + "/search",
                new Dictionary<string, string> { { "catara", query ?? string.Empty }, { "konuara", konuara } },
                searchRule);
        }

        var url = BuildCatalogUrl(path, query, searchType);
        var rule = path switch
        {
            "latest" => ToScrapeRule(Settings.Latest),
            "popular" => Settings.Popular,
            "latestmovies" => Settings.LatestMovies,
            "dubbed" or "cartoons" or "subbed" or "movies" or "ova" or "genre" => Settings.Catalog,
            _ => Settings.EpisodesPage
        };

        var html = await _httpService.GetAsync(url);
        var items = ParseCatalogItems(html, rule);

        // A series URL may be served with either the old list markup or the new
        // #episodeList markup; fall back to the other rule when the chosen one yields nothing.
        if (items.Count == 0 && rule != Settings.Catalog)
            items = ParseCatalogItems(html, Settings.Catalog);

        return items;
    }

    public override async Task<EpisodeListData> GetEpisodesAsync(string url)
    {
        var fullUrl = EnsureFullUrl(url);
        var html = await _httpService.GetAsync(fullUrl);

        // New series pages use #episodeList; older ones use the "name=pid" list.
        var rule = Settings.EpisodesPage;
        var dataSection = ExtractBetween(html, rule.Start, rule.End);
        if (string.IsNullOrEmpty(dataSection))
        {
            rule = Settings.Episode;
            dataSection = ExtractBetween(html, rule.Start, rule.End);
        }

        if (string.IsNullOrEmpty(dataSection))
            return new EpisodeListData();

        var episodes = new List<EpisodeItem>();
        var regex = new Regex(rule.Regex, RegexOptions.Singleline);

        foreach (Match match in regex.Matches(dataSection))
        {
            var link = match.Groups["link"].Value;
            var name = match.Groups["name"].Value;
            var type = match.Groups["type"].Success ? match.Groups["type"].Value : null;

            if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(name))
                continue;

            episodes.Add(new EpisodeItem
            {
                Url = EnsureFullUrl(link),
                Title = UnescapeHtml(name),
                Type = type
            });
        }

        var metadata = await GetPageMetadataAsync(fullUrl);
        
        return new EpisodeListData
        {
            Episodes = episodes,
            Plot = metadata.Plot,
            ThumbnailUrl = metadata.ThumbnailUrl
        };
    }

    public override async Task<PageMetadata> GetPageMetadataAsync(string url)
    {
        var fullUrl = EnsureFullUrl(url);
        var html = await _httpService.GetAsync(fullUrl);
        
        string? plot = null;
        string? thumbnail = null;

        var thumbSection = ExtractBetween(html, Settings.Thumbnail.Start, Settings.Thumbnail.End);
        if (!string.IsNullOrEmpty(thumbSection))
        {
            var endQuote = thumbSection.IndexOf('"');
            if (endQuote > 0)
            {
                thumbnail = EnsureFullUrl(thumbSection.Substring(0, endQuote));
                if (thumbnail.Contains("/wp-content"))
                {
                    thumbnail = thumbnail.Replace(BaseUrl + "/wp-content", ImagesUrl);
                }
            }
        }

        var plotSection = ExtractBetween(html, Settings.PagePlot.Start, Settings.PagePlot.End);
        if (!string.IsNullOrEmpty(plotSection))
        {
            var plotMatch = Regex.Match(plotSection, Settings.PagePlot.Regex, RegexOptions.Singleline);
            if (plotMatch.Success && plotMatch.Groups.Count > 1)
            {
                plot = UnescapeHtml(plotMatch.Groups[1].Value.Trim());
            }
        }

        return new PageMetadata { Plot = plot, ThumbnailUrl = thumbnail };
    }

    public override async Task<string?> ResolveVideoUrlAsync(string url)
    {
        return await ResolveVideoUrlCoreAsync(EnsureFullUrl(url));
    }

    private async Task<string?> ResolveIframeAsync(string iframeUrl)
    {
        try
        {
            var html = await _httpService.GetAsync(iframeUrl);
            
            var videoMatches = ExtractMatches(html, @"source\s+src=""([^""]+)""", m => m.Groups[1].Value);
            if (videoMatches.Count > 0)
                return videoMatches[0];

            var m3u8Matches = ExtractMatches(html, @"([""'])(https?://[^""']+\.m3u8[^""']*)\1", m => m.Groups[2].Value);
            if (m3u8Matches.Count > 0)
                return m3u8Matches[0];

            var mp4Matches = ExtractMatches(html, @"([""'])(https?://[^""']+\.mp4[^""']*)\1", m => m.Groups[2].Value);
            if (mp4Matches.Count > 0)
                return mp4Matches[0];
        }
        catch
        {
        }

        return null;
    }

    private List<string> ExtractDirectVideoUrls(string html)
    {
        var urls = new List<string>();

        var m3u8Matches = ExtractMatches(html, @"([""'])(https?://[^""']+\.m3u8[^""']*)\1", m => m.Groups[2].Value);
        urls.AddRange(m3u8Matches);

        var mp4Matches = ExtractMatches(html, @"([""'])(https?://[^""']+\.mp4[^""']*)\1", m => m.Groups[2].Value);
        urls.AddRange(mp4Matches);

        return urls;
    }

    private string BuildCatalogUrl(string path, string? query, string? searchType)
    {
        return path switch
        {
            "latest" => BaseUrl + Settings.Latest.Url,
            "popular" => BaseUrl,
            "search" when query != null && searchType == "series" => 
                $"{BaseUrl}/search?catara={Uri.EscapeDataString(query)}&konuara=series",
            "search" when query != null && searchType == "movies" => 
                $"{BaseUrl}/movie-list",
            "search" when query != null && searchType == "episodes" => 
                $"{BaseUrl}/search?catara={Uri.EscapeDataString(query)}&konuara=episodes",
            _ => BuildCatalogPathUrl(path)
        };
    }
}