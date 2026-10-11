using Jellyfin.Plugin.WatchNixtoons2.Configuration;
using Jellyfin.Plugin.WatchNixtoons2.Services;

namespace Jellyfin.Plugin.WatchNixtoons2.Scrapers;

public enum SiteType
{
    Wcostream = 0,
    Wcoflix = 1,
    Wcoforever = 2
}

public static class ScraperFactory
{
    private static readonly Dictionary<SiteType, string> SiteDomains = new()
    {
        { SiteType.Wcostream, "www.wcostream.tv" },
        { SiteType.Wcoflix, "www.wcoflix.tv" },
        { SiteType.Wcoforever, "www.wcoforever.net" }
    };

    public static BaseScraper CreateScraper(SiteType siteType, HttpService httpService)
    {
        var domain = SiteDomains[siteType];
        var baseUrl = $"https://{domain}";

        return siteType switch
        {
            SiteType.Wcostream => new WcostreamScraper(httpService, baseUrl),
            SiteType.Wcoflix => new WcoflixScraper(httpService, baseUrl),
            SiteType.Wcoforever => new WcoforeverScraper(httpService, baseUrl),
            _ => throw new ArgumentException($"Unknown site type: {siteType}")
        };
    }

    public static string GetDomain(SiteType siteType) => SiteDomains[siteType];

    /// <summary>
    /// The default site. Wcoforever is used because it is the mirror the plugin
    /// libraries are tuned against and it is reachable from the Jellyfin container
    /// (www.wcostream.tv is not: it answers 403 to Jellyfin's egress).
    /// </summary>
    public static SiteType GetDefaultSiteType() => SiteType.Wcoforever;

    /// <summary>
    /// Resolves the configured site. Accepts either a SiteType name or a site URL /
    /// host; anything unrecognised falls back to <see cref="GetDefaultSiteType"/>.
    /// </summary>
    public static SiteType ResolveSiteType(string? baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl) && Enum.TryParse<SiteType>(baseUrl.Trim(), true, out var parsed))
            return parsed;

        var value = baseUrl?.ToLowerInvariant() ?? string.Empty;
        if (value.Contains("wcoflix"))
            return SiteType.Wcoflix;
        if (value.Contains("wcoforever") || value.Contains("wcofun"))
            return SiteType.Wcoforever;
        if (value.Contains("wcostream"))
            return SiteType.Wcostream;

        return GetDefaultSiteType();
    }
}