using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.WatchNixtoons2.Configuration;

namespace Jellyfin.Plugin.WatchNixtoons2;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public override string Name => "WatchNixtoons2";

    public override string Description => "Watch cartoons and anime from wcofun.net and related sites";

    public override Guid Id => Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "WatchNixtoons2",
                EmbeddedResourcePath = "Jellyfin.Plugin.WatchNixtoons2.Configuration.Resources.configPage.html"
            },
            new PluginPageInfo
            {
                Name = "WatchNixtoons2 Browser",
                EmbeddedResourcePath = "Jellyfin.Plugin.WatchNixtoons2.Configuration.Resources.browser.html",
                EnableInMainMenu = true,
                MenuSection = "plugins",
                MenuIcon = "video_library",
                DisplayName = "WatchNixtoons2"
            }
        };
    }
}
