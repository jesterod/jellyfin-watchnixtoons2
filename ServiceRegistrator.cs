using System;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Plugin.WatchNixtoons2.Configuration;
using Jellyfin.Plugin.WatchNixtoons2.Controllers;
using Jellyfin.Plugin.WatchNixtoons2.Scrapers;
using Jellyfin.Plugin.WatchNixtoons2.Services;

namespace Jellyfin.Plugin.WatchNixtoons2;

/// <summary>
/// Registers the plugin's services with Jellyfin's dependency injection container.
/// </summary>
/// <remarks>
/// This is deliberately a separate type from <see cref="Plugin"/>. Jellyfin 10.10
/// discovers IPluginServiceRegistrator implementations and creates them with
/// Activator.CreateInstance, which requires a public parameterless constructor.
/// The Plugin type cannot provide one, so registration lives here.
/// </remarks>
public class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton(provider => new HttpService(GetConfig()));
        serviceCollection.AddSingleton(provider => new WatchNixtoons2Service(GetConfig()));
        serviceCollection.AddSingleton(provider =>
        {
            var config = GetConfig();
            var httpService = provider.GetRequiredService<HttpService>();
            var scraper = ScraperFactory.CreateScraper(GetSiteType(config), httpService);
            return new CatalogService(httpService, config, scraper);
        });
        serviceCollection.AddSingleton(provider =>
        {
            var config = GetConfig();
            var httpService = provider.GetRequiredService<HttpService>();
            var scraper = ScraperFactory.CreateScraper(GetSiteType(config), httpService);
            return new VideoResolver(scraper);
        });
        serviceCollection.AddSingleton(provider => new RecentlyWatchedService(GetConfig()));
        serviceCollection.AddSingleton<MediaBrowser.Controller.Channels.IChannel, WatchNixtoons2Channel>();
        serviceCollection.AddSingleton<WatchNixtoons2Channel>();

        // Jellyfin 10.10 does not register the plugin instance in DI, but controllers
        // (e.g. ConfigurationController) construct with it. Resolve lazily via Instance.
        serviceCollection.AddSingleton<Plugin>(provider => Plugin.Instance!);

        serviceCollection.AddControllers()
            .AddApplicationPart(typeof(WatchNixtoons2Controller).Assembly);
    }

    private static PluginConfiguration GetConfig()
        => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    private static SiteType GetSiteType(PluginConfiguration config)
        => ScraperFactory.ResolveSiteType(config.BaseUrl);
}
