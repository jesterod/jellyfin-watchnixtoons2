using Microsoft.AspNetCore.Mvc;
using Jellyfin.Plugin.WatchNixtoons2.Configuration;
using Jellyfin.Plugin.WatchNixtoons2.Services;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.WatchNixtoons2.Controllers;

[ApiController]
[Route("WatchNixtoons2/[action]")]
public class ConfigurationController : ControllerBase
{
    private readonly Plugin _plugin;

    public ConfigurationController(Plugin plugin)
    {
        _plugin = plugin;
    }

    [HttpGet]
    public ActionResult<PluginConfiguration> GetConfiguration()
    {
        return Ok(_plugin.Configuration);
    }

    [HttpPost]
    public ActionResult SaveConfiguration([FromBody] PluginConfiguration config)
    {
        _plugin.Configuration.BaseUrl = config.BaseUrl;
        _plugin.Configuration.ShowCatalog = config.ShowCatalog;
        _plugin.Configuration.ShowLatestThumbs = config.ShowLatestThumbs;
        _plugin.Configuration.ShowPopularThumbs = config.ShowPopularThumbs;
        _plugin.Configuration.ShowSeriesThumbs = config.ShowSeriesThumbs;
        _plugin.Configuration.ShowVideoFanart = config.ShowVideoFanart;
        _plugin.Configuration.ShowCatalogThumbs = config.ShowCatalogThumbs;
        _plugin.Configuration.UseLatestDate = config.UseLatestDate;
        _plugin.Configuration.CleanupEpisodes = config.CleanupEpisodes;
        _plugin.Configuration.ReverseEpisodes = config.ReverseEpisodes;
        _plugin.Configuration.PlaybackMethod = config.PlaybackMethod;
        _plugin.Configuration.RequestDelay = config.RequestDelay;

        _plugin.SaveConfiguration();
        return Ok();
    }
}