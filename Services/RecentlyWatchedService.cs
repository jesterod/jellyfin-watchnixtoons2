using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.WatchNixtoons2.Configuration;

namespace Jellyfin.Plugin.WatchNixtoons2.Services;

public class RecentlyWatchedService
{
    private readonly string _dataFilePath;
    private readonly ConcurrentDictionary<string, RecentlyWatchedItem> _items = new();
    private readonly object _fileLock = new();

    public RecentlyWatchedService(PluginConfiguration config)
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "jellyfin", "plugins", "WatchNixtoons2");
        
        Directory.CreateDirectory(dataDir);
        _dataFilePath = Path.Combine(dataDir, "recently_watched.json");
        
        Load();
    }

    public IReadOnlyList<RecentlyWatchedItem> GetAll()
    {
        return _items.Values
            .OrderByDescending(x => x.WatchedAt)
            .ToList();
    }

    public void Add(string name, string url)
    {
        var cleanUrl = CleanUrl(url);
        var hash = GenerateHash(cleanUrl);

        var item = new RecentlyWatchedItem
        {
            Name = name,
            Url = cleanUrl,
            WatchedAt = DateTime.UtcNow
        };

        _items[hash] = item;
        Save();
    }

    public bool Remove(string url)
    {
        var cleanUrl = CleanUrl(url);
        var hash = GenerateHash(cleanUrl);
        
        var removed = _items.TryRemove(hash, out _);
        if (removed)
        {
            Save();
        }
        return removed;
    }

    public void Clear()
    {
        _items.Clear();
        Save();
    }

    private void Load()
    {
        if (!File.Exists(_dataFilePath))
            return;

        try
        {
            var json = File.ReadAllText(_dataFilePath);
            var items = JsonSerializer.Deserialize<Dictionary<string, RecentlyWatchedItem>>(json);
            
            if (items != null)
            {
                foreach (var kvp in items)
                {
                    _items[kvp.Key] = kvp.Value;
                }
            }
        }
        catch
        {
            // Ignore corrupted file
        }
    }

    private void Save()
    {
        lock (_fileLock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_items, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_dataFilePath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }

    private static string CleanUrl(string url)
    {
        // Remove base domain from URL for storage
        var domains = new[] { "www.wcostream.tv", "www.wcoflix.tv", "www.wcoforever.net" };
        foreach (var domain in domains)
        {
            if (url.Contains(domain))
            {
                var uri = new Uri(url);
                return uri.PathAndQuery;
            }
        }
        return url;
    }

    private static string GenerateHash(string input)
    {
        using var md5 = MD5.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

public class RecentlyWatchedItem
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime WatchedAt { get; set; }
}