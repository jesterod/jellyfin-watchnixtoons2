using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Jellyfin.Plugin.WatchNixtoons2.Models;
using Jellyfin.Plugin.WatchNixtoons2.Services;

namespace Jellyfin.Plugin.WatchNixtoons2.Scrapers;

public abstract class BaseScraper
{
    protected readonly SiteSettings Settings;
    protected readonly string BaseUrl;
    protected readonly string ImagesUrl = "https://images.wcostream.com";
    protected readonly string ThumbsBaseUrl = "https://doko-desuka.github.io/128h/";

    protected BaseScraper(SiteSettings settings, string baseUrl)
    {
        Settings = settings;
        BaseUrl = baseUrl;
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
        var regex = new Regex(pattern, RegexOptions.Singleline);
        foreach (Match match in regex.Matches(html))
        {
            if (match.Groups.Count > 1)
                matches.Add(match.Groups[1].Value);
            else
                matches.Add(match.Value);
        }
        return matches;
    }

    protected List<T> ExtractMatches<T>(string html, string pattern, Func<Match, T> selector)
    {
        var results = new List<T>();
        var regex = new Regex(pattern, RegexOptions.Singleline);
        foreach (Match match in regex.Matches(html))
        {
            results.Add(selector(match));
        }
        return results;
    }

    public abstract Task<List<CatalogItem>> GetCatalogAsync(string path, string? query = null, string? searchType = null);

    public abstract Task<EpisodeListData> GetEpisodesAsync(string url);

    public abstract Task<PageMetadata> GetPageMetadataAsync(string url);

    public abstract Task<string?> ResolveVideoUrlAsync(string url);
}