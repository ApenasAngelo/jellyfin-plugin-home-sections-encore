using Jellyfin.Plugin.HomeScreenSections.Encore;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Model.Dto;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections.Encore;

/// <summary>
/// Rows picked from the catalog, for every account, even without watch history. Which rows appear and what they show
/// is a draw that lasts the day, and titles the user finished stay out.
/// </summary>
public abstract class EncoreCatalogRowSection : EncoreSectionBase
{
    private const int c_minimumItems = 8;

    private readonly EncoreCatalog m_catalog;
    private readonly EncoreHistory m_history;

    protected EncoreCatalogRowSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService, EncoreCatalog catalog, EncoreHistory history)
        : base(userManager, libraryManager, dtoService)
    {
        m_catalog = catalog;
        m_history = history;
    }

    /// <summary>The rows that can appear today.</summary>
    protected abstract IReadOnlyList<EncoreRow> Rows { get; }

    public override QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload, IQueryCollection queryCollection)
    {
        User? user = UserManager.GetUserById(payload.UserId);
        EncoreRow? row = Rows.FirstOrDefault(x => x.Key == payload.AdditionalData) ?? (Rows.Count == 1 ? Rows[0] : null);
        if (user == null || row == null)
        {
            return new QueryResult<BaseItemDto>();
        }

        HashSet<Guid> finished = m_history.Get(user).Finished;
        List<EncoreEntry> matches = m_catalog.Entries.Where(x => row.Match(x) && !finished.Contains(x.Id)).ToList();

        // The draw favors the better rated part of the row.
        int poolSize = Math.Max(RowSize * 2, matches.Count * 6 / 10);
        IEnumerable<Guid> pool = matches.OrderByDescending(x => x.Rating).Take(poolSize).Select(x => x.Id);

        return ReadItems(user, EncoreDraw.Shuffle(pool, user.Id, Section + row.Key));
    }

    public override IEnumerable<IHomeScreenSection> CreateInstances(Guid? userId, int instanceCount)
    {
        if (userId is null || userId.Value.Equals(default) || UserManager.GetUserById(userId.Value) == null)
        {
            yield break;
        }

        IReadOnlyList<EncoreEntry> entries = m_catalog.Entries;
        IEnumerable<EncoreRow> available = Rows.Where(row => entries.Count(row.Match) >= c_minimumItems);

        foreach (EncoreRow row in EncoreDraw.Shuffle(available, userId.Value, Section!).Take(instanceCount))
        {
            EncoreCatalogRowSection instance = (EncoreCatalogRowSection)MemberwiseClone();
            instance.AdditionalData = row.Key;
            instance.DisplayText = row.Title;
            yield return instance;
        }
    }

    protected static Func<EncoreEntry, bool> Genre(params string[] genres) => x => genres.Any(x.Genres.Contains);

    protected static Func<EncoreEntry, bool> Tag(params string[] tags) => x => tags.Any(x.Tags.Contains);
}
