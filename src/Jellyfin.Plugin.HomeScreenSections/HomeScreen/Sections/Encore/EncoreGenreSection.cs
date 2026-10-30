using Jellyfin.Extensions;
using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Helpers;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Model.Dto;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections.Encore;

/// <summary>
/// A row of movies from a genre of the catalog. Unlike the Genre section it needs no watch history, so a new account
/// sees it too, and it treats the genre names a library holds in several languages ("Action", "Ação") as one genre.
/// </summary>
public class EncoreGenreSection : IHomeScreenSection
{
    private const int c_minimumItems = 8;

    private static readonly (string Title, string[] Names)[] s_genres =
    {
        ("Filmes de ação", new[] { "Ação", "Action" }),
        ("Filmes de aventura", new[] { "Aventura", "Adventure" }),
        ("Animações", new[] { "Animação", "Animation" }),
        ("Comédias", new[] { "Comédia", "Comedy" }),
        ("Filmes de crime", new[] { "Crime" }),
        ("Documentários", new[] { "Documentário", "Documentary" }),
        ("Dramas", new[] { "Drama" }),
        ("Para ver em família", new[] { "Família", "Family" }),
        ("Filmes de fantasia", new[] { "Fantasia", "Fantasy" }),
        ("Ficção científica", new[] { "Ficção científica", "Science Fiction", "Sci-Fi" }),
        ("Filmes de guerra", new[] { "Guerra", "War" }),
        ("Filmes de mistério", new[] { "Mistério", "Mystery" }),
        ("Romances", new[] { "Romance" }),
        ("Filmes de terror", new[] { "Terror", "Horror" }),
        ("Suspense", new[] { "Thriller" }),
    };

    public string? Section => "EncoreGenre";
    public string? DisplayText { get; set; } = "Genre (no history needed)";
    public string? AdminDescription => "Rows of movies from random genres of the catalog, for every account, even without watch history.";
    public int? Limit => s_genres.Length;
    public string? Route => "originalpayload";
    public string? AdditionalData { get; set; }
    public object? OriginalPayload { get; set; }

    private readonly IUserManager m_userManager;
    private readonly ILibraryManager m_libraryManager;
    private readonly IDtoService m_dtoService;

    public EncoreGenreSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService)
    {
        m_userManager = userManager;
        m_libraryManager = libraryManager;
        m_dtoService = dtoService;
    }

    public QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload, IQueryCollection queryCollection)
    {
        (string Title, string[] Names) genre = s_genres.FirstOrDefault(x => x.Title == payload.AdditionalData);
        if (genre.Names == null)
        {
            return new QueryResult<BaseItemDto>();
        }

        User? user = m_userManager.GetUserById(payload.UserId);
        DtoOptions dtoOptions = new DtoOptions
        {
            Fields = new[] { ItemFields.PrimaryImageAspectRatio, ItemFields.MediaSourceCount }
        };

        List<BaseItem> movies = QueryMovies(user, genre.Names, 24, dtoOptions).ToList();
        movies.Shuffle();

        return new QueryResult<BaseItemDto>(m_dtoService.GetBaseItemDtos(movies.Take(16).ToArray(), dtoOptions, user));
    }

    public IEnumerable<IHomeScreenSection> CreateInstances(Guid? userId, int instanceCount)
    {
        User? user = userId is null || userId.Value.Equals(default) ? null : m_userManager.GetUserById(userId.Value);
        if (user == null)
        {
            yield break;
        }

        (string Title, string[] Names)[] shuffled = s_genres.OrderBy(_ => Random.Shared.Next()).ToArray();
        int created = 0;
        foreach ((string Title, string[] Names) genre in shuffled)
        {
            if (created >= instanceCount)
            {
                yield break;
            }

            if (QueryMovies(user, genre.Names, c_minimumItems, new DtoOptions(false)).Count() < c_minimumItems)
            {
                continue;
            }

            created++;
            yield return new EncoreGenreSection(m_userManager, m_libraryManager, m_dtoService)
            {
                AdditionalData = genre.Title,
                DisplayText = genre.Title,
                OriginalPayload = m_dtoService.GetBaseItemDto(m_libraryManager.GetGenre(genre.Names[0]), new DtoOptions(), user)
            };
        }
    }

    public HomeScreenSectionInfo GetInfo()
    {
        return new HomeScreenSectionInfo
        {
            Section = Section,
            DisplayText = DisplayText,
            AdditionalData = AdditionalData,
            Route = Route,
            Limit = Limit ?? 1,
            OriginalPayload = OriginalPayload,
            ViewMode = SectionViewMode.Portrait,
            PluginConfigurationOptions = (this as IHomeScreenSection).GetPluginConfigurationOptions().ToArray()
        };
    }

    private IEnumerable<BaseItem> QueryMovies(User? user, string[] genres, int limit, DtoOptions dtoOptions)
    {
        VirtualFolderInfo[] folders = m_libraryManager.GetVirtualFolders()
            .Where(x => x.CollectionType == CollectionTypeOptions.movies)
            .FilterToUserPermitted(m_libraryManager, user);

        return folders.SelectMany(x =>
        {
            Guid folderId = Guid.Parse(x.ItemId ?? Guid.Empty.ToString());
            if (m_libraryManager.GetParentItem(folderId, user?.Id) is not Folder folder)
            {
                folder = m_libraryManager.GetUserRootFolder();
            }

            return folder.GetItems(new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                OrderBy = new[] { (ItemSortBy.Random, SortOrder.Descending) },
                ParentId = folderId,
                Recursive = true,
                Limit = limit,
                DtoOptions = dtoOptions,
                Genres = genres
            }).Items;
        }).DistinctBy(x => x.Id);
    }
}
