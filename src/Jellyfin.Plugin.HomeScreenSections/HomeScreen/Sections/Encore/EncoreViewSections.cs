using Jellyfin.Data.Enums;
using Jellyfin.Plugin.HomeScreenSections.Encore;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Model.Dto;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections.Encore;

/// <summary>What the user stopped in the middle, among the titles of a library or collection.</summary>
public class EncoreViewContinueSection : EncoreSectionBase
{
    private const int c_lookAt = 60;

    private readonly EncoreCatalog m_catalog;

    public EncoreViewContinueSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService, EncoreCatalog catalog)
        : base(userManager, libraryManager, dtoService)
    {
        m_catalog = catalog;
        DisplayText = "Continuar assistindo";
    }

    public override string? Section => "EncoreViewContinue";

    public override string? AdminDescription => "Used by the library and collection pages; leave it off the home screen.";

    public override QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload, IQueryCollection queryCollection)
    {
        User? user = UserManager.GetUserById(payload.UserId);
        IReadOnlySet<Guid>? scope = m_catalog.ScopeOf(queryCollection);
        if (user == null || scope == null)
        {
            return new QueryResult<BaseItemDto>();
        }

        IEnumerable<Guid> items = LibraryManager.GetItemList(new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
            IsResumable = true,
            Recursive = true,
            OrderBy = new[] { (ItemSortBy.DatePlayed, SortOrder.Descending) },
            Limit = c_lookAt,
            DtoOptions = new DtoOptions(false)
        })
        .Where(x => scope.Contains(x is Episode episode ? episode.SeriesId : (x as Video)?.PrimaryVersionId ?? x.Id))
        .Select(x => (x as Video)?.PrimaryVersionId ?? x.Id);

        return ReadItems(user, items);
    }
}

/// <summary>The next episode of each series in progress, among the titles of a library or collection.</summary>
public class EncoreViewNextUpSection : EncoreSectionBase
{
    private readonly EncoreCatalog m_catalog;
    private readonly EncoreHistory m_history;

    public EncoreViewNextUpSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService, EncoreCatalog catalog, EncoreHistory history)
        : base(userManager, libraryManager, dtoService)
    {
        m_catalog = catalog;
        m_history = history;
        DisplayText = "Próximos episódios";
    }

    public override string? Section => "EncoreViewNextUp";

    public override string? AdminDescription => "Used by the library and collection pages; leave it off the home screen.";

    public override QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload, IQueryCollection queryCollection)
    {
        User? user = UserManager.GetUserById(payload.UserId);
        IReadOnlySet<Guid>? scope = m_catalog.ScopeOf(queryCollection);
        if (user == null || scope == null)
        {
            return new QueryResult<BaseItemDto>();
        }

        IEnumerable<Guid> episodes = m_history.Get(user).Series
            .Where(x => x.Remaining > 0 && x.NextEpisodeId != null && scope.Contains(x.Id))
            .OrderByDescending(x => x.LastPlayed)
            .Select(x => x.NextEpisodeId!.Value);

        return ReadItems(user, episodes);
    }
}

/// <summary>A draw of the titles of one collection: its row in the Collections page, and the first row of its own page.</summary>
public class EncoreCollectionRowSection : EncoreSectionBase
{
    private readonly EncoreCatalog m_catalog;
    private readonly EncoreHistory m_history;

    public EncoreCollectionRowSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService, EncoreCatalog catalog, EncoreHistory history)
        : base(userManager, libraryManager, dtoService)
    {
        m_catalog = catalog;
        m_history = history;
        DisplayText = "Collection";
    }

    public override string? Section => "EncoreCollectionRow";

    public override string? AdminDescription => "Used by the Collections page; leave it off the home screen.";

    // More than one row of this section can share a page, and the client tells them apart by AdditionalData.
    public override int? Limit => 2;

    public override QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload, IQueryCollection queryCollection)
    {
        User? user = UserManager.GetUserById(payload.UserId);
        if (user == null || !Guid.TryParse(payload.AdditionalData, out Guid collection))
        {
            return new QueryResult<BaseItemDto>();
        }

        IReadOnlySet<Guid> scope = m_catalog.Scope(collection);
        HashSet<Guid> finished = m_history.Get(user).Finished;
        List<EncoreEntry> matches = m_catalog.Entries.Where(x => scope.Contains(x.Id) && !finished.Contains(x.Id)).ToList();
        int poolSize = Math.Max(RowSize * 2, matches.Count * 6 / 10);
        IEnumerable<Guid> pool = matches.OrderByDescending(x => x.Rating).Take(poolSize).Select(x => x.Id);

        return ReadItems(user, EncoreDraw.Shuffle(pool, user.Id, Section + collection.ToString("N")));
    }

    public override IEnumerable<IHomeScreenSection> CreateInstances(Guid? userId, int instanceCount)
    {
        yield break;
    }

    public HomeScreenSectionInfo Row(Guid collection, string title)
    {
        EncoreCollectionRowSection instance = (EncoreCollectionRowSection)MemberwiseClone();
        instance.AdditionalData = collection.ToString("N");
        instance.DisplayText = title;
        return instance.GetInfo();
    }
}
