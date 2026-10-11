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

    public static SiteType GetDefaultSiteType() => SiteType.Wcostream;
}