using System.Collections.Generic;

namespace Jellyfin.Plugin.WatchNixtoons2.Models;

public class SiteSettings
{
    public ScrapeRule Catalog { get; set; } = new();

    public ScrapeRule Episode { get; set; } = new();

    public ScrapeRule EpisodesPage { get; set; } = new();

    public ScrapeRule SeriesSearch { get; set; } = new();

    public ScrapeRule MovieSearch { get; set; } = new();

    public ScrapeRule EpisodeSearch { get; set; } = new();

    public ScrapeRule Genre { get; set; } = new();

    public ScrapeRule Thumbnail { get; set; } = new();

    public ScrapeRule PageMeta { get; set; } = new();

    public ScrapeRule PagePlot { get; set; } = new();

    public LatestSettings Latest { get; set; } = new();

    public ScrapeRule LatestMovies { get; set; } = new();

    public ScrapeRule Popular { get; set; } = new();

    public ScrapeRule Parent { get; set; } = new();

    public ScrapeRule Chapter { get; set; } = new();

    public bool DecodeSourceRequired { get; set; }
}

public class ScrapeRule
{
    public string Regex { get; set; } = string.Empty;

    public string Start { get; set; } = string.Empty;

    public string? StartAlt { get; set; }

    public string End { get; set; } = string.Empty;
}

public class LatestSettings
{
    public string Url { get; set; } = string.Empty;

    public string Regex { get; set; } = string.Empty;

    public string Start { get; set; } = string.Empty;

    public string End { get; set; } = string.Empty;
}

public class UrlPaths
{
    public string Latest { get; set; } = "latest";

    public string Popular { get; set; } = "popular";

    public string Dubbed { get; set; } = "/dubbed-anime-list";

    public string Cartoons { get; set; } = "/cartoon-list";

    public string Subbed { get; set; } = "/subbed-anime-list";

    public string Movies { get; set; } = "/movie-list";

    public string LatestMovies { get; set; } = "/anime/movies";

    public string Ova { get; set; } = "/ova-list";

    public string Search { get; set; } = "/search";

    public string Genre { get; set; } = "/search-by-genre";
}