using Jellyfin.Data.Enums;
using Jellyfin.Plugin.HomeScreenSections.Encore;
using Jellyfin.Plugin.HomeScreenSections.Model.Dto;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections.Encore;

/// <summary>The user's favorites. Not cached: marking a favorite has to show on the next visit, and the query is cheap.</summary>
public class EncoreMyListSection : EncoreSectionBase
{
    public EncoreMyListSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService)
        : base(userManager, libraryManager, dtoService)
    {
        DisplayText = "Minha lista";
    }

    public override string? Section => "EncoreMyList";

    public override string? AdminDescription => "The user's favorite movies and series.";

    public override QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload, IQueryCollection queryCollection)
    {
        User? user = UserManager.GetUserById(payload.UserId);
        if (user == null)
        {
            return new QueryResult<BaseItemDto>();
        }

        IEnumerable<Guid> favorites = LibraryManager.GetItemList(new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series },
            IsFavorite = true,
            Recursive = true,
            OrderBy = new[] { (ItemSortBy.SortName, SortOrder.Ascending) },
            Limit = RowSize + 8,
            DtoOptions = new DtoOptions(false)
        }).Select(x => x.Id);

        return ReadItems(user, favorites);
    }
}

/// <summary>Series the user is close to finishing, shown by the next episode.</summary>
public class EncoreAlmostDoneSection : EncoreSectionBase
{
    private const int c_maximumRemaining = 5;

    private readonly EncoreHistory m_history;

    public EncoreAlmostDoneSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService, EncoreHistory history)
        : base(userManager, libraryManager, dtoService)
    {
        m_history = history;
        DisplayText = "Faltam poucos episódios";
    }

    public override string? Section => "EncoreAlmostDone";

    public override string? AdminDescription => "Series with five aired episodes or fewer left to watch.";

    public override QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload, IQueryCollection queryCollection)
    {
        User? user = UserManager.GetUserById(payload.UserId);
        if (user == null)
        {
            return new QueryResult<BaseItemDto>();
        }

        IEnumerable<Guid> episodes = m_history.Get(user).Series
            .Where(x => x.Remaining is > 0 and <= c_maximumRemaining && x.NextEpisodeId != null)
            .OrderByDescending(x => x.LastPlayed)
            .Select(x => x.NextEpisodeId!.Value);

        return ReadItems(user, episodes);
    }
}

/// <summary>
/// Movies and finished series watched a while ago. Replaces the Watch Again section, which queried every series of the
/// library: this one starts from what the user watched.
/// </summary>
public class EncoreWatchAgainSection : EncoreSectionBase
{
    private const int c_minimumEpisodes = 3;

    private static readonly TimeSpan s_minimumAge = TimeSpan.FromDays(28);

    private readonly EncoreHistory m_history;

    public EncoreWatchAgainSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService, EncoreHistory history)
        : base(userManager, libraryManager, dtoService)
    {
        m_history = history;
        DisplayText = "Assistir de novo";
    }

    public override string? Section => "EncoreWatchAgain";

    public override string? AdminDescription => "Movies and finished series watched more than 28 days ago.";

    public override QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload, IQueryCollection queryCollection)
    {
        User? user = UserManager.GetUserById(payload.UserId);
        if (user == null)
        {
            return new QueryResult<BaseItemDto>();
        }

        DateTime cutoff = DateTime.UtcNow - s_minimumAge;
        EncoreWatchHistory history = m_history.Get(user);
        IEnumerable<Guid> candidates = history.Movies
            .Where(x => x.LastPlayed < cutoff)
            .Select(x => x.Id)
            .Concat(history.Series
                .Where(x => x.Remaining == 0 && x.Played >= c_minimumEpisodes && x.LastPlayed < cutoff)
                .Select(x => x.Id));

        return ReadItems(user, EncoreDraw.Shuffle(candidates, user.Id, Section!));
    }
}

/// <summary>A daily draw from the user's weekly recommendations.</summary>
public class EncoreForYouSection : EncoreSectionBase
{
    private readonly EncoreRecommendations m_recommendations;
    private readonly EncoreHistory m_history;

    public EncoreForYouSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService, EncoreRecommendations recommendations, EncoreHistory history)
        : base(userManager, libraryManager, dtoService)
    {
        m_recommendations = recommendations;
        m_history = history;
        DisplayText = "Para você";
    }

    public override string? Section => "EncoreForYou";

    public override string? AdminDescription => "A daily draw from a list made every week from what the user watched. Empty without watch history.";

    public override QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload, IQueryCollection queryCollection)
    {
        User? user = UserManager.GetUserById(payload.UserId);
        if (user == null)
        {
            return new QueryResult<BaseItemDto>();
        }

        HashSet<Guid> started = m_history.Get(user).Started;
        IEnumerable<Guid> pool = m_recommendations.GetList(user).Where(x => !started.Contains(x));

        return ReadItems(user, EncoreDraw.Shuffle(pool, user.Id, Section!));
    }
}
