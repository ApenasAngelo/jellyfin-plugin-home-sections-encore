using Jellyfin.Data.Enums;
using Jellyfin.Plugin.HomeScreenSections.Helpers;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HomeScreenSections.Encore;

/// <summary>What the rows need to know about a title, kept in memory.</summary>
public sealed record EncoreEntry(Guid Id, bool IsSeries, bool IsAnime, float Rating, HashSet<string> Genres, HashSet<string> Tags, bool IsBrazilian);

/// <summary>
/// The titles of the libraries, without the alternate versions of a title. Rows that need no watch history pick from
/// this list, so a row costs one query for the items it shows. Refreshed in the background once a day.
/// </summary>
public class EncoreCatalog
{
    private const string c_versionTag = "gelato-stream";

    // The catalog mixes TMDB names in English and in Portuguese, and the TV ones join two genres in one.
    private static readonly Dictionary<string, string[]> s_genreAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ação"] = new[] { "action" },
        ["Action"] = new[] { "action" },
        ["Aventura"] = new[] { "adventure" },
        ["Adventure"] = new[] { "adventure" },
        ["Action & Adventure"] = new[] { "action", "adventure" },
        ["Animação"] = new[] { "animation" },
        ["Animation"] = new[] { "animation" },
        ["Comédia"] = new[] { "comedy" },
        ["Comedy"] = new[] { "comedy" },
        ["Crime"] = new[] { "crime" },
        ["Documentário"] = new[] { "documentary" },
        ["Documentary"] = new[] { "documentary" },
        ["Drama"] = new[] { "drama" },
        ["Família"] = new[] { "family" },
        ["Family"] = new[] { "family" },
        ["Kids"] = new[] { "family" },
        ["Fantasia"] = new[] { "fantasy" },
        ["Fantasy"] = new[] { "fantasy" },
        ["Ficção científica"] = new[] { "scifi" },
        ["Science Fiction"] = new[] { "scifi" },
        ["Sci-Fi"] = new[] { "scifi" },
        ["Sci-Fi & Fantasy"] = new[] { "scifi", "fantasy" },
        ["Guerra"] = new[] { "war" },
        ["War"] = new[] { "war" },
        ["War & Politics"] = new[] { "war" },
        ["Mistério"] = new[] { "mystery" },
        ["Mystery"] = new[] { "mystery" },
        ["Romance"] = new[] { "romance" },
        ["Terror"] = new[] { "horror" },
        ["Horror"] = new[] { "horror" },
        ["Thriller"] = new[] { "thriller" },
        ["História"] = new[] { "history" },
        ["History"] = new[] { "history" },
    };

    private readonly ILibraryManager m_libraryManager;
    private readonly ILogger<EncoreCatalog> m_logger;
    private readonly object m_lock = new object();
    private IReadOnlyList<EncoreEntry>? m_entries;
    private DateTime m_builtAt = DateTime.MinValue;
    private int m_refreshing;

    public EncoreCatalog(ILibraryManager libraryManager, ILogger<EncoreCatalog> logger)
    {
        m_libraryManager = libraryManager;
        m_logger = logger;
    }

    public IReadOnlyList<EncoreEntry> Entries
    {
        get
        {
            IReadOnlyList<EncoreEntry>? entries = m_entries;
            if (entries == null)
            {
                lock (m_lock)
                {
                    return m_entries ??= Build();
                }
            }

            if (DateTime.UtcNow - m_builtAt > TimeSpan.FromDays(1) && Interlocked.Exchange(ref m_refreshing, 1) == 0)
            {
                Task.Run(() =>
                {
                    try
                    {
                        Refresh();
                    }
                    catch (Exception e)
                    {
                        m_logger.LogError(e, "Encore: could not refresh the catalog");
                    }
                    finally
                    {
                        Interlocked.Exchange(ref m_refreshing, 0);
                    }
                });
            }

            return entries;
        }
    }

    public void Refresh()
    {
        IReadOnlyList<EncoreEntry> entries = Build();
        lock (m_lock)
        {
            m_entries = entries;
        }
    }

    private IReadOnlyList<EncoreEntry> Build()
    {
        DateTime start = DateTime.UtcNow;
        List<EncoreEntry> entries = new List<EncoreEntry>();
        HashSet<Guid> seen = new HashSet<Guid>();

        foreach (VirtualFolderInfo folder in m_libraryManager.GetVirtualFolders().Where(x => x.CollectionType != CollectionTypeOptions.boxsets))
        {
            if (!Guid.TryParse(folder.ItemId, out Guid folderId))
            {
                continue;
            }

            bool mixed = folder.IsMixedFolder(m_libraryManager);
            IReadOnlyList<BaseItem> items = m_libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series },
                AncestorIds = new[] { folderId },
                Recursive = true,
                ExcludeTags = new[] { c_versionTag },
                DtoOptions = new DtoOptions(false)
            });

            foreach (BaseItem item in items)
            {
                if (item is Video { PrimaryVersionId: not null } || !seen.Add(item.Id))
                {
                    continue;
                }

                HashSet<string> tags = (item.Tags ?? Array.Empty<string>()).Select(x => x.ToLowerInvariant()).ToHashSet();
                entries.Add(new EncoreEntry(
                    item.Id,
                    item is not MediaBrowser.Controller.Entities.Movies.Movie,
                    mixed || tags.Contains("anime"),
                    item.CommunityRating ?? 0,
                    (item.Genres ?? Array.Empty<string>()).SelectMany(CanonicalGenres).ToHashSet(),
                    tags,
                    (item.ProductionLocations ?? Array.Empty<string>()).Any(x => x.Equals("Brazil", StringComparison.OrdinalIgnoreCase) || x.Equals("Brasil", StringComparison.OrdinalIgnoreCase))));
            }
        }

        m_builtAt = DateTime.UtcNow;
        m_logger.LogInformation("Encore: catalog built with {Count} titles in {Seconds:0.0} s", entries.Count, (DateTime.UtcNow - start).TotalSeconds);
        return entries;
    }

    private static IEnumerable<string> CanonicalGenres(string genre)
    {
        return s_genreAliases.TryGetValue(genre, out string[]? canonical) ? canonical : new[] { genre.ToLowerInvariant() };
    }
}
