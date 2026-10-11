using System.Collections.Generic;

namespace Jellyfin.Plugin.WatchNixtoons2.Models;

public class CatalogItem
{
    public string Url { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    public string? Plot { get; set; }

    public bool IsFolder { get; set; }

    public bool IsPlayable { get; set; }

    public Dictionary<string, string>? ExtraParams { get; set; }
}

public class CatalogSection
{
    public string Name { get; set; } = string.Empty;

    public List<CatalogItem> Items { get; set; } = new();
}

public class EpisodeItem
{
    public string Url { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Type { get; set; }
}

public class EpisodeListData
{
    public string? ThumbnailUrl { get; set; }

    public string? Plot { get; set; }

    public List<EpisodeItem> Episodes { get; set; } = new();
}

public class PageMetadata
{
    public string? Plot { get; set; }

    public string? ThumbnailUrl { get; set; }
}