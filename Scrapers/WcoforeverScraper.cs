using System.Text.RegularExpressions;
using Jellyfin.Plugin.WatchNixtoons2.Models;
using Jellyfin.Plugin.WatchNixtoons2.Services;

namespace Jellyfin.Plugin.WatchNixtoons2.Scrapers;

public class WcoforeverScraper : BaseScraper
{
    private readonly HttpService _httpService;

    public WcoforeverScraper(HttpService httpService, string baseUrl) 
        : base(CreateSettings(), baseUrl)
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
                Regex = @"<a href=""(?P<link>[^""]+).*?(data-lang=""(?P<type>[^""]+)"")?>(?:<span>)?(?P<name>[^<]+)",
                Start = @"name=""pid""",
                End = @"""sidebar-all"""
            },
            SeriesSearch = new ScrapeRule
            {
                Regex = @"<a\s*href=""(?P<link>[^""]+).*?>([^<]+)\s*<img alt=""(?P<name>[^""]+)""\s*src=""(?P<img>[^""]+)",
                Start = @"""submit",
                End = @"<script>"
            },
            MovieSearch = new ScrapeRule
            {
                Regex = @"<a href=""(?P<link>[^""]+).*?>(?P<name>[^<]+)",
                Start = @"""ddmcc""",
                End = @"/ul></ul"
            },
            EpisodeSearch = new ScrapeRule
            {
                Regex = @"<a href=""(?P<link>[^""]+)[^>]*>(?P<name>[^<]+)</a",
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
                Regex = @"src=""(?P<img>[^""]+)"">\s*</a>\s*</div>\s*<div\s*class=""recent-release-episodes""><a\s*href=""(?P<link>[^""]+)""\s*rel=""bookmark"">(?P<name>.*?)</a",
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
        var url = BuildCatalogUrl(path, query, searchType);
        var html = await _httpService.GetAsync(url);
        
        var dataSection = ExtractBetween(html, Settings.Catalog.Start, Settings.Catalog.End);
        if (string.IsNullOrEmpty(dataSection))
            return new List<CatalogItem>();

        var items = new List<CatalogItem>();
        var regex = new Regex(Settings.Catalog.Regex, RegexOptions.Singleline);
        
        foreach (Match match in regex.Matches(dataSection))
        {
            if (match.Groups.Count >= 3)
            {
                var link = match.Groups[1].Value;
                var name = match.Groups[2].Value;
                
                items.Add(new CatalogItem
                {
                    Url = EnsureFullUrl(link),
                    Title = UnescapeHtml(name),
                    IsFolder = true
                });
            }
        }

        return items;
    }

    public override async Task<EpisodeListData> GetEpisodesAsync(string url)
    {
        var fullUrl = EnsureFullUrl(url);
        var html = await _httpService.GetAsync(fullUrl);
        
        var dataSection = ExtractBetween(html, Settings.Episode.Start, Settings.Episode.End);
        if (string.IsNullOrEmpty(dataSection))
            return new EpisodeListData();

        var episodes = new List<EpisodeItem>();
        var regex = new Regex(Settings.Episode.Regex, RegexOptions.Singleline);
        
        foreach (Match match in regex.Matches(dataSection))
        {
            var link = match.Groups["link"].Value;
            var name = match.Groups["name"].Value;
            var type = match.Groups["type"].Success ? match.Groups["type"].Value : null;
            
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
        var fullUrl = EnsureFullUrl(url);
        var html = await _httpService.GetAsync(fullUrl);
        
        var chapterMatches = ExtractMatches(html, Settings.Chapter.Regex, m => m.Groups[1].Value);
        if (chapterMatches.Count > 0)
        {
            foreach (var iframeUrl in chapterMatches)
            {
                var resolved = await ResolveIframeAsync(iframeUrl);
                if (!string.IsNullOrEmpty(resolved))
                    return resolved;
            }
        }

        var directUrls = ExtractDirectVideoUrls(html);
        if (directUrls.Count > 0)
            return directUrls[0];

        return null;
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
            "genre" => BaseUrl + path,
            _ => BaseUrl + path
        };
    }
}