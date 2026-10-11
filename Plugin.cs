using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Plugin.WatchNixtoons2.Configuration;
using Jellyfin.Plugin.WatchNixtoons2.Services;
using Jellyfin.Plugin.WatchNixtoons2.Scrapers;
using Jellyfin.Plugin.WatchNixtoons2.Controllers;

namespace Jellyfin.Plugin.WatchNixtoons2;

public class Plugin : BasePlugin<PluginConfiguration>, IPluginServiceRegistrator, IHasWebPages
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

    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        var config = Configuration;
        
        serviceCollection.AddSingleton<HttpService>(provider => new HttpService(config));
        serviceCollection.AddSingleton<WatchNixtoons2Service>(provider => 
            new WatchNixtoons2Service(config));
        serviceCollection.AddSingleton<CatalogService>(provider => 
        {
            var httpService = provider.GetRequiredService<HttpService>();
            var scraper = ScraperFactory.CreateScraper(
                Enum.TryParse<SiteType>(config.BaseUrl, out var parsed) ? parsed : SiteType.Wcostream,
                httpService);
            return new CatalogService(httpService, config, scraper);
        });
        serviceCollection.AddSingleton<VideoResolver>(provider => 
        {
            var httpService = provider.GetRequiredService<HttpService>();
            var scraper = ScraperFactory.CreateScraper(
                Enum.TryParse<SiteType>(config.BaseUrl, out var parsed) ? parsed : SiteType.Wcostream,
                httpService);
            return new VideoResolver(scraper);
        });
        serviceCollection.AddSingleton<RecentlyWatchedService>(provider => 
            new RecentlyWatchedService(config));

        serviceCollection.AddControllers()
            .AddApplicationPart(typeof(WatchNixtoons2Controller).Assembly);
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "WatchNixtoons2",
                EmbeddedResourcePath = "Jellyfin.Plugin.WatchNixtoons2.Configuration.Resources.configPage.html"
            }
        };
    }
}