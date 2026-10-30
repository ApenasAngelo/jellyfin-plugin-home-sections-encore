using System.Collections.Concurrent;
using System.Text.Json;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HomeScreenSections.Encore;

/// <summary>
/// A long list of titles for each user, made once a week from what the user watched and kept on disk. The row draws
/// from it every day. A user with no list yet, or whose list is too old, gets one on the first request.
/// </summary>
public class EncoreRecommendations
{
    private const int c_listSize = 120;

    // Tags that say nothing about taste.
    private static readonly HashSet<string> s_ignoredTags = new()
    {
        "anime", "gelato-stream", "extra-curto", "duringcreditsstinger", "aftercreditsstinger", "sequel", "based on manga", "woman director"
    };

    private readonly EncoreCatalog m_catalog;
    private readonly EncoreHistory m_history;
    private readonly IUserManager m_userManager;
    private readonly ILogger<EncoreRecommendations> m_logger;
    private readonly string m_path;
    private readonly ConcurrentDictionary<Guid, StoredList> m_lists;
    private readonly object m_saveLock = new object();

    public EncoreRecommendations(EncoreCatalog catalog, EncoreHistory history, IUserManager userManager, IApplicationPaths paths, ILogger<EncoreRecommendations> logger)
    {
        m_catalog = catalog;
        m_history = history;
        m_userManager = userManager;
        m_logger = logger;
        m_path = Path.Combine(paths.PluginConfigurationsPath, typeof(HomeScreenSectionsPlugin).Namespace!, "encore-recommendations.json");
        m_lists = new ConcurrentDictionary<Guid, StoredList>(Load());
    }

    public IReadOnlyList<Guid> GetList(User user)
    {
        if (m_lists.TryGetValue(user.Id, out StoredList? list) && DateTime.UtcNow - list.GeneratedAt < TimeSpan.FromDays(8))
        {
            return list.Ids;
        }

        return Generate(user);
    }

    public void GenerateAll(CancellationToken cancellationToken)
    {
        m_catalog.Refresh();
        foreach (User user in m_userManager.GetUsers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            Generate(user);
        }
    }

    private Guid[] Generate(User user)
    {
        EncoreWatchHistory history = m_history.Get(user);
        if (history.IsEmpty)
        {
            // Nothing is stored: the first title the user watches already produces a list.
            m_lists.TryRemove(user.Id, out _);
            return Array.Empty<Guid>();
        }

        Dictionary<Guid, EncoreEntry> catalog = m_catalog.Entries.ToDictionary(x => x.Id);
        Dictionary<string, double> taste = new Dictionary<string, double>();
        foreach (Guid id in history.Started)
        {
            if (catalog.TryGetValue(id, out EncoreEntry? entry))
            {
                foreach (string feature in Features(entry))
                {
                    taste[feature] = taste.GetValueOrDefault(feature) + 1;
                }
            }
        }

        Guid[] ids = catalog.Values
            .Where(x => !history.Started.Contains(x.Id))
            .Select(x => (x.Id, Score: Score(x, taste)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(c_listSize)
            .Select(x => x.Id)
            .ToArray();

        m_lists[user.Id] = new StoredList(DateTime.UtcNow, ids);
        Save();
        return ids;
    }

    private static double Score(EncoreEntry entry, Dictionary<string, double> taste)
    {
        List<string> features = Features(entry).ToList();
        if (features.Count == 0)
        {
            return 0;
        }

        // The root keeps a title with many tags from winning just for having many tags.
        return features.Sum(x => taste.GetValueOrDefault(x)) / Math.Sqrt(features.Count) * entry.Rating;
    }

    private static IEnumerable<string> Features(EncoreEntry entry)
    {
        return entry.Genres.Select(x => "g:" + x).Concat(entry.Tags.Where(x => !s_ignoredTags.Contains(x)).Select(x => "t:" + x));
    }

    private Dictionary<Guid, StoredList> Load()
    {
        try
        {
            if (File.Exists(m_path))
            {
                return JsonSerializer.Deserialize<Dictionary<Guid, StoredList>>(File.ReadAllText(m_path)) ?? new Dictionary<Guid, StoredList>();
            }
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            m_logger.LogWarning(e, "Encore: ignoring an unreadable recommendations file");
        }

        return new Dictionary<Guid, StoredList>();
    }

    private void Save()
    {
        lock (m_saveLock)
        {
            string temporary = m_path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(m_path)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(m_lists));
            File.Move(temporary, m_path, true);
        }
    }

    private sealed record StoredList(DateTime GeneratedAt, Guid[] Ids);
}
