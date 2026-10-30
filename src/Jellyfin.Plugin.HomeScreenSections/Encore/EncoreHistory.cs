using Jellyfin.Data.Enums;
using Jellyfin.Plugin.HomeScreenSections.Services;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Plugin.HomeScreenSections.Encore;

public sealed record EncoreWatchedMovie(Guid Id, DateTime? LastPlayed);

/// <summary>A series the user started. <c>Remaining</c> stops counting at <see cref="EncoreHistory.RemainingCap"/>.</summary>
public sealed record EncoreSeriesProgress(Guid Id, int Played, int Remaining, DateTime? LastPlayed, Guid? NextEpisodeId);

public sealed record EncoreWatchHistory(IReadOnlyList<EncoreWatchedMovie> Movies, IReadOnlyList<EncoreSeriesProgress> Series)
{
    public HashSet<Guid> Finished { get; } = Movies.Select(x => x.Id).Concat(Series.Where(x => x.Remaining == 0).Select(x => x.Id)).ToHashSet();

    public HashSet<Guid> Started { get; } = Movies.Select(x => x.Id).Concat(Series.Select(x => x.Id)).ToHashSet();

    public bool IsEmpty => Movies.Count == 0 && Series.Count == 0;
}

/// <summary>
/// What a user has watched, starting from the played items: the cost grows with the user's history and not with the
/// size of the library. Cached until the user finishes or marks something as played.
/// </summary>
public class EncoreHistory
{
    public const int RemainingCap = 6;

    private const string c_versionTag = "gelato-stream";

    private readonly ILibraryManager m_libraryManager;
    private readonly IUserDataManager m_userDataManager;
    private readonly PerUserComputedStatsCache m_statsCache;

    public EncoreHistory(ILibraryManager libraryManager, IUserDataManager userDataManager, PerUserComputedStatsCache statsCache)
    {
        m_libraryManager = libraryManager;
        m_userDataManager = userDataManager;
        m_statsCache = statsCache;
    }

    public EncoreWatchHistory Get(User user)
    {
        return m_statsCache.TryGetOrCompute(user.Id, "encore-history", TimeSpan.FromDays(1), () => Compute(user), out EncoreWatchHistory history)
            ? history
            : new EncoreWatchHistory(Array.Empty<EncoreWatchedMovie>(), Array.Empty<EncoreSeriesProgress>());
    }

    private EncoreWatchHistory Compute(User user)
    {
        List<EncoreWatchedMovie> movies = Played(user, BaseItemKind.Movie)
            .Select(x => new EncoreWatchedMovie((x as Video)?.PrimaryVersionId ?? x.Id, m_userDataManager.GetUserData(user, x)?.LastPlayedDate))
            .ToList();

        List<EncoreSeriesProgress> series = Played(user, BaseItemKind.Episode)
            .OfType<Episode>()
            .Where(x => x.ParentIndexNumber != 0 && x.SeriesId != Guid.Empty)
            .GroupBy(x => x.SeriesId)
            .Select(group =>
            {
                List<BaseItem> next = NextEpisodes(user, group.First());
                return new EncoreSeriesProgress(
                    group.Key,
                    group.Select(x => (x.ParentIndexNumber, x.IndexNumber)).Distinct().Count(),
                    next.Count,
                    group.Max(x => m_userDataManager.GetUserData(user, x)?.LastPlayedDate),
                    next.FirstOrDefault()?.Id);
            })
            .ToList();

        return new EncoreWatchHistory(movies, series);
    }

    private IReadOnlyList<BaseItem> Played(User user, BaseItemKind kind)
    {
        return m_libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { kind },
            IsPlayed = true,
            Recursive = true,
            DtoOptions = new DtoOptions(false)
        });
    }

    private List<BaseItem> NextEpisodes(User user, Episode played)
    {
        return m_libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            SeriesPresentationUniqueKey = played.SeriesPresentationUniqueKey,
            IsPlayed = false,
            IsMissing = false,
            IsVirtualItem = false,
            ParentIndexNumberNotEquals = 0,
            MaxPremiereDate = DateTime.UtcNow,
            ExcludeTags = new[] { c_versionTag },
            OrderBy = new[] { (ItemSortBy.ParentIndexNumber, SortOrder.Ascending), (ItemSortBy.IndexNumber, SortOrder.Ascending) },
            Limit = RemainingCap,
            EnableTotalRecordCount = false,
            DtoOptions = new DtoOptions(false)
        }).ToList();
    }
}
