using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Jellyfin.Plugin.WatchNixtoons2.Models;
using Jellyfin.Plugin.WatchNixtoons2.Services;

namespace Jellyfin.Plugin.WatchNixtoons2.Scrapers;

public abstract class BaseScraper
{
    protected readonly SiteSettings Settings;
    protected readonly string BaseUrl;
    protected readonly HttpService Http;
    protected readonly string ImagesUrl = "https://images.wcostream.com";
    protected readonly string ThumbsBaseUrl = "https://doko-desuka.github.io/128h/";

    /// <summary>
    /// Friendly catalog keys (as used by the front end) mapped to the real path on the
    /// site. An empty value means the site's home page.
    /// </summary>
    private static readonly Dictionary<string, string> CatalogPaths = new()
    {
        { "latest", string.Empty },
        { "popular", string.Empty },
        { "dubbed", "/dubbed-anime-list" },
        { "cartoons", "/cartoon-list" },
        { "subbed", "/subbed-anime-list" },
        { "movies", "/movie-list" },
        { "latestmovies", "/anime/movies" },
        { "ova", "/ova-list" },
        { "genre", "/search-by-genre" },
        { "search", "/search" }
    };

    protected BaseScraper(SiteSettings settings, string baseUrl, HttpService httpService)
    {
        Settings = settings;
        BaseUrl = baseUrl;
        Http = httpService;
    }

    protected string EnsureFullUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        if (url.StartsWith("//"))
            return "https:" + url;

        if (url.StartsWith("http"))
            return url;

        if (url.StartsWith("/"))
            return BaseUrl + url;

        return BaseUrl + "/" + url;
    }

    /// <summary>
    /// Builds an absolute URL for a catalog path. Translates the friendly keys used by
    /// the front end and guarantees a path separator, so a key can never be concatenated
    /// directly onto the host (which previously produced hosts such as
    /// "www.wcostream.tvgenre").
    /// </summary>
    protected string BuildCatalogPathUrl(string path)
    {
        if (string.IsNullOrEmpty(path))
            return BaseUrl;

        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return path;

        if (!CatalogPaths.TryGetValue(path, out var mapped))
            mapped = path;

        if (string.IsNullOrEmpty(mapped))
            return BaseUrl;

        return BaseUrl + (mapped.StartsWith("/") ? mapped : "/" + mapped);
    }

    protected string EnsureUrlSchema(string url)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        if (url.StartsWith("//"))
            return "https:" + url;

        return url;
    }

    protected string RemoveBaseUrl(string url)
    {
        if (url.StartsWith(BaseUrl))
            return url[BaseUrl.Length..];
        return url;
    }

    protected string UnescapeHtml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return text
            .Replace("&", "&")
            .Replace("\"", "\"")
            .Replace("&apos;", "'")
            .Replace("&#038;", "&")
            .Replace("&#039;", "\u2019")
            .Replace("&#160;", " ")
            .Replace("&#8211;", "\u2013")
            .Replace("&#8216;", "\u2018")
            .Replace("&#8217;", "\u2019")
            .Replace("&#8220;", "\u201C")
            .Replace("&#8221;", "\u201D")
            .Replace("&#8230;", "\u2026")
            .Replace("\u2606", " ");
    }

    protected HtmlDocument LoadHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    protected string? ExtractBetween(string html, string startMarker, string endMarker)
    {
        var startIndex = html.IndexOf(startMarker, StringComparison.Ordinal);
        if (startIndex == -1)
            return null;

        startIndex += startMarker.Length;
        var endIndex = string.IsNullOrEmpty(endMarker) 
            ? html.Length 
            : html.IndexOf(endMarker, startIndex, StringComparison.Ordinal);

        if (endIndex == -1)
            endIndex = html.Length;

        return html.Substring(startIndex, endIndex - startIndex);
    }

    protected List<string> ExtractMatches(string html, string pattern)
    {
        var matches = new List<string>();
        var matches2 = Regex.Matches(html, pattern, RegexOptions.Singleline);
        foreach (Match match in matches2)
        {
            matches.Add(match.Groups.Count > 1 ? match.Groups[1].Value : match.Value);
        }
        return matches;
    }

    protected List<string> ExtractMatches(string html, string pattern, Func<Match, string> selector)
    {
        var results = new List<string>();
        var regex = new Regex(pattern, RegexOptions.Singleline);
        foreach (Match match in regex.Matches(html))
        {
            results.Add(selector(match));
        }

        return results;
    }

    /// <summary>
    /// Extracts catalog entries from a page using the supplied scrape rule. Supports both
    /// named groups (<c>link</c>/<c>name</c>) and positional groups (1/2) so every site
    /// rule can be parsed with a single implementation.
    /// </summary>
    protected List<CatalogItem> ParseCatalogItems(string html, ScrapeRule rule)
    {
        var items = new List<CatalogItem>();
        if (string.IsNullOrEmpty(rule.Regex))
            return items;

        var section = ExtractBetween(html, rule.Start, rule.End);
        if (string.IsNullOrEmpty(section))
            return items;

        var regex = new Regex(rule.Regex, RegexOptions.Singleline);
        foreach (Match match in regex.Matches(section))
        {
            var link = match.Groups["link"].Success
                ? match.Groups["link"].Value
                : (match.Groups.Count > 1 ? match.Groups[1].Value : string.Empty);
            var name = match.Groups["name"].Success
                ? match.Groups["name"].Value
                : (match.Groups.Count > 2 ? match.Groups[2].Value : string.Empty);

            if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(name))
                continue;

            items.Add(new CatalogItem
            {
                Url = EnsureFullUrl(link.Trim()),
                Title = UnescapeHtml(name.Trim()),
                IsFolder = true
            });
        }

        return items;
    }

    protected async Task<List<CatalogItem>> FetchCatalogAsync(string url, ScrapeRule rule)
    {
        var html = await Http.GetAsync(url);
        return ParseCatalogItems(html, rule);
    }

    protected async Task<List<CatalogItem>> PostCatalogAsync(string url, Dictionary<string, string> formData, ScrapeRule rule)
    {
        var html = await Http.PostAsync(url, formData, new Dictionary<string, string> { { "Referer", BaseUrl + "/" } });
        return ParseCatalogItems(html, rule);
    }

    protected static ScrapeRule ToScrapeRule(LatestSettings latest)
        => new ScrapeRule { Regex = latest.Regex, Start = latest.Start, End = latest.End };

    /// <summary>
    /// Resolves a playable media URL from an episode page. The site hides the stream behind
    /// an iframe + an ad-gated player, so the chain is:
    /// episode page -&gt; iframe (embed host) -&gt; video-js.php -&gt; getvidlink.php (JSON) -&gt;
    /// getvid (JSON) -&gt; direct media URL.
    /// </summary>
    protected async Task<string?> ResolveVideoUrlCoreAsync(string pageUrl)
    {
        var html = await Http.GetAsync(pageUrl);

        var iframeUrl = ExtractPlayerIframe(html);
        if (string.IsNullOrEmpty(iframeUrl))
            return null;

        // Skip the ad-gate: request the real player with the iframe's query string.
        var playerUrl = iframeUrl.Contains("/index.php", StringComparison.OrdinalIgnoreCase)
            ? iframeUrl.Replace("/index.php", "/video-js.php", StringComparison.OrdinalIgnoreCase)
            : iframeUrl;

        var playerHtml = await Http.GetAsync(playerUrl, new Dictionary<string, string> { { "Referer", BaseUrl + "/" } });

        var linkMatch = Regex.Match(playerHtml, @"getvidlink\.php\?([^""'\s]+)");
        if (!linkMatch.Success)
            return null;

        var origin = new Uri(playerUrl).GetLeftPart(UriPartial.Authority);
        var linkUrl = origin + "/inc/embed/getvidlink.php?" + System.Net.WebUtility.HtmlDecode(linkMatch.Groups[1].Value);

        var linkJson = await Http.GetAsync(linkUrl, new Dictionary<string, string>
        {
            { "Referer", playerUrl },
            { "X-Requested-With", "XMLHttpRequest" }
        });

        string? server;
        string? token;
        using (var doc = JsonDocument.Parse(linkJson))
        {
            server = doc.RootElement.TryGetProperty("server", out var s) ? s.GetString() : null;
            token = doc.RootElement.TryGetProperty("enc", out var e) ? e.GetString() : null;
        }

        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(token))
            return null;

        var getVidUrl = server.TrimEnd('/') + "/getvid?evid=" + token + "&json";
        var finalJson = await Http.GetAsync(getVidUrl, new Dictionary<string, string>
        {
            { "Referer", playerUrl },
            { "X-Requested-With", "XMLHttpRequest" }
        });

        using var finalDoc = JsonDocument.Parse(finalJson);
        return finalDoc.RootElement.ValueKind == JsonValueKind.String ? finalDoc.RootElement.GetString() : null;
    }

    private static string? ExtractPlayerIframe(string html)
    {
        Match? fallback = null;
        foreach (Match match in Regex.Matches(html, @"<iframe[^>]*\bsrc=""([^""]+)""", RegexOptions.Singleline))
        {
            fallback ??= match;
            var src = match.Groups[1].Value;
            if (src.Contains("/inc/embed/", StringComparison.OrdinalIgnoreCase) ||
                src.Contains("embed.", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeUrl(src);
            }
        }

        return fallback is null ? null : NormalizeUrl(fallback.Groups[1].Value);
    }

    private static string? NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        url = System.Net.WebUtility.HtmlDecode(url.Trim());
        if (url.StartsWith("//"))
            return "https:" + url;

        return url;
    }

    public abstract Task<List<CatalogItem>> GetCatalogAsync(string path, string? query = null, string? searchType = null);

    public abstract Task<EpisodeListData> GetEpisodesAsync(string url);

    public abstract Task<PageMetadata> GetPageMetadataAsync(string url);

    public abstract Task<string?> ResolveVideoUrlAsync(string url);
}
