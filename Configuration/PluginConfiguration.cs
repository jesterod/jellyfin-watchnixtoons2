using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.WatchNixtoons2.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string BaseUrl { get; set; } = "https://www.wcofun.net";

    public bool ShowCatalog { get; set; } = true;

    public bool ShowLatestThumbs { get; set; } = true;

    public bool ShowPopularThumbs { get; set; } = true;

    public bool ShowSeriesThumbs { get; set; } = true;

    public bool ShowVideoFanart { get; set; } = true;

    public bool UseLatestDate { get; set; } = false;

    public bool ShowCatalogThumbs { get; set; } = true;

    public bool CleanupEpisodes { get; set; } = false;

    public bool ReverseEpisodes { get; set; } = false;

    public string PlaybackMethod { get; set; } = "Direct";

    public int RequestDelay { get; set; } = 1500;
}