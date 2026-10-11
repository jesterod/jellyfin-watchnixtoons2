using Jellyfin.Plugin.WatchNixtoons2.Scrapers;

namespace Jellyfin.Plugin.WatchNixtoons2.Services;

public class VideoResolver
{
    private readonly BaseScraper _scraper;

    public VideoResolver(BaseScraper scraper)
    {
        _scraper = scraper;
    }

    public async Task<string?> ResolveAsync(string url, bool playChapters = false)
    {
        if (playChapters)
        {
            // For chapters, we might want to return a playlist or handle differently
            // For now, just resolve the main video
        }

        return await _scraper.ResolveVideoUrlAsync(url);
    }

    public async Task<List<string>> ResolveAllSourcesAsync(string url)
    {
        var sources = new List<string>();
        var mainUrl = await _scraper.ResolveVideoUrlAsync(url);
        
        if (!string.IsNullOrEmpty(mainUrl))
        {
            sources.Add(mainUrl);
        }

        return sources;
    }
}