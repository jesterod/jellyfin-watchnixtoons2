using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Jellyfin.Plugin.WatchNixtoons2.Models;
using Jellyfin.Plugin.WatchNixtoons2.Services;

namespace Jellyfin.Plugin.WatchNixtoons2.Scrapers;

public class WcostreamScraper : BaseScraper
{
    private readonly HttpService _httpService;

    public WcostreamScraper(HttpService httpService, string baseUrl) 
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
                Regex = @"<li(?:\sdata\-id=""[0-9]+""?)?>\s*<a href=""([^""]+)"".*?>([^<]+)",
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
            SeriesSearch = new ScrapeRule
            {
                Regex = @"<a\s*href=""(?<link>[^""]+).*?>([^<]+)\s*<img alt=""(?<name>[^""]+)""\s*src=""(?<img>[^""]+)",
                Start = @"""aramamotoru",
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
                Regex = @"<a.*?""([^""]+).*?>(.*?)</",
                Start = @"ddmcc"">",
                End = @"</div></div>"
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
                Start = @"""lalyx""",
                End = string.Empty
            },
            PagePlot = new ScrapeRule
            {
                Regex = @"class=""iltext""><p>(.*?)</p>",
                Start = @"katcont",
                End = string.Empty
            },
            Latest = new LatestSettings
            {
                Url = string.Empty,
                Regex = @"src=""(?<img>[^""]+)"">\s*</a>\s*</div>\s*<div class=""recent-release-episodes"">\s*<a href=""(?<link>[^""]+)"" rel=""bookmark"">(?<name>[^<]+)</a>",
                Start = @"<div class=""recent-release"">",
                End = @"</ul>"
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
                Start = @"class=""menustyle"">",
                End = @"</div>"
            },
            Parent = new ScrapeRule
            {
                Regex = @"class=""ildate"">\s*<a href=""([^""]+)""(?:[^>]+)?><span(?:[^>]+)>([^/<]+)<",
                Start = @"""lalyx""",
                End = string.Empty
            },
            Chapter = new ScrapeRule
            {
                Regex = @"<iframe id=""(?:[a-zA-Z]+)uploads(?:[0-9]+)"" src=""([^""]+)""",
                Start = string.Empty,
                End = string.Empty
            },
            DecodeSourceRequired = false
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
            _ => Settings.Catalog
        };

        return await FetchCatalogAsync(url, rule);
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

        // Extract thumbnail
        var thumbSection = ExtractBetween(html, Settings.Thumbnail.Start, Settings.Thumbnail.End);
        if (!string.IsNullOrEmpty(thumbSection))
        {
            var endQuote = thumbSection.IndexOf('"');
            if (endQuote > 0)
            {
                thumbnail = EnsureFullUrl(thumbSection.Substring(0, endQuote));
                // Replace wp-content with images URL
                if (thumbnail.Contains("/wp-content"))
                {
                    thumbnail = thumbnail.Replace(BaseUrl + "/wp-content", ImagesUrl);
                }
            }
        }

        // Extract plot
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

    private string? TryPremiumWorkaround(string html)
    {
        var playlistMatch = Regex.Match(html, @"<a href=""([^""]+)""[^>]*>Watch on Playlist</a>");
        if (!playlistMatch.Success)
            return null;

        var playlistUrl = playlistMatch.Groups[1].Value;
        playlistUrl = playlistUrl.Replace("/playlist-cat/", "/playlist-cat-jw/");
        
        // Use wcoforever domain for playlist
        var wcoforeverDomain = "www.wcoforever.net";
        if (playlistUrl.StartsWith("http"))
        {
            playlistUrl = playlistUrl.Replace(BaseUrl, $"https://{wcoforeverDomain}");
        }
        else
        {
            playlistUrl = $"https://{wcoforeverDomain}{playlistUrl}";
        }

        try
        {
            var playlistHtml = _httpService.GetAsync(playlistUrl).Result;
            var mediaIdMatch = Regex.Match(playlistHtml, @"if\s*\(mediaid === ""([0-9]+)""\)");
            if (!mediaIdMatch.Success)
                return null;

            var mediaId = mediaIdMatch.Groups[1].Value;
            var playlistUrlMatch = Regex.Match(playlistHtml, @"playlist: ""(/playlist-cat-rss/[0-9]+\?[^""]+)""");
            if (!playlistUrlMatch.Success)
                return null;

            var rssUrl = playlistUrlMatch.Groups[1].Value;
            rssUrl = rssUrl.Replace(wcoforeverDomain, new Uri(BaseUrl).Host);
            if (!rssUrl.StartsWith("http"))
                rssUrl = BaseUrl + rssUrl;

            var rssXml = _httpService.GetAsync(rssUrl).Result;
            var videoMatch = Regex.Match(rssXml, $@"<mediaid>{mediaId}</mediaid>\s*<jwplayer:image>(?:[^<]+)</jwplayer:image>\s*<jwplayer:source file=""([^""]+)""");
            
            if (videoMatch.Success)
                return videoMatch.Groups[1].Value;
        }
        catch
        {
            // Ignore errors in workaround
        }

        return null;
    }

    private async Task<string?> ResolveIframeAsync(string iframeUrl)
    {
        try
        {
            var html = await _httpService.GetAsync(iframeUrl);
            
            // Look for video sources in iframe
            var videoMatches = ExtractMatches(html, @"source\s+src=""([^""]+)""", m => m.Groups[1].Value);
            if (videoMatches.Count > 0)
                return videoMatches[0];

            // Try m3u8
            var m3u8Matches = ExtractMatches(html, @"([""'])(https?://[^""']+\.m3u8[^""']*)\1", m => m.Groups[2].Value);
            if (m3u8Matches.Count > 0)
                return m3u8Matches[0];

            // Try mp4
            var mp4Matches = ExtractMatches(html, @"([""'])(https?://[^""']+\.mp4[^""']*)\1", m => m.Groups[2].Value);
            if (mp4Matches.Count > 0)
                return mp4Matches[0];
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    private List<string> ExtractDirectVideoUrls(string html)
    {
        var urls = new List<string>();

        // m3u8
        var m3u8Matches = ExtractMatches(html, @"([""'])(https?://[^""']+\.m3u8[^""']*)\1", m => m.Groups[2].Value);
        urls.AddRange(m3u8Matches);

        // mp4
        var mp4Matches = ExtractMatches(html, @"([""'])(https?://[^""']+\.mp4[^""']*)\1", m => m.Groups[2].Value);
        urls.AddRange(mp4Matches);

        return urls;
    }

    private string BuildCatalogUrl(string path, string? query, string? searchType)
    {
        return path switch
        {
            "latest" => BaseUrl,
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