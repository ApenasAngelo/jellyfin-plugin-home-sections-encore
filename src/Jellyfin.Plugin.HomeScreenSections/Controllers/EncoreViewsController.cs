using System.Security.Cryptography;
using System.Text;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Encore;
using Jellyfin.Plugin.HomeScreenSections.Helpers;
using Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections.Encore;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Model;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.HomeScreenSections.Controllers;

/// <summary>The rows of the library and collection pages, which are the home page showing only some titles.</summary>
[ApiController]
[Route("HomeScreen/Encore")]
public class EncoreViewsController : ControllerBase
{
    private readonly IHomeScreenManager m_homeScreenManager;
    private readonly ILibraryManager m_libraryManager;
    private readonly IUserManager m_userManager;
    private readonly EncoreCatalog m_catalog;

    public EncoreViewsController(IHomeScreenManager homeScreenManager, ILibraryManager libraryManager, IUserManager userManager, EncoreCatalog catalog)
    {
        m_homeScreenManager = homeScreenManager;
        m_libraryManager = libraryManager;
        m_userManager = userManager;
        m_catalog = catalog;
    }

    /// <summary>What the client needs to send library and collection links to these pages.</summary>
    [HttpGet("Views")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Authorize]
    public ActionResult<object> GetViews([FromQuery] Guid? userId)
    {
        if (!UserAuthorizationHelper.TryGetUserId(User, m_userManager, userId, out Guid authenticatedUserId) || m_userManager.GetUserById(authenticatedUserId) is not { } user)
        {
            return Forbid();
        }

        return Ok(new
        {
            Enabled = HomeScreenSectionsPlugin.Instance.Configuration.Enabled,
            Collections = VisibleCollections(user).Select(x => x.Id.ToString("N")),
            CollectionsLibrary = m_libraryManager.GetVirtualFolders().FirstOrDefault(x => x.CollectionType == CollectionTypeOptions.boxsets)?.ItemId
        });
    }

    [HttpGet("View")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Authorize]
    public ActionResult<QueryResult<HomeScreenSectionInfo>> GetView(
        [FromQuery] Guid? userId,
        [FromQuery] Guid view,
        [FromQuery] Guid? pageHash = null,
        [FromQuery] int page = 1,
        [FromQuery] int? numResultsPerPage = null)
    {
        if (!UserAuthorizationHelper.TryGetUserId(User, m_userManager, userId, out Guid authenticatedUserId) || m_userManager.GetUserById(authenticatedUserId) is not { } user)
        {
            return Forbid();
        }

        List<HomeScreenSectionInfo> rows = Rows(user, view, pageHash ?? Guid.Empty);
        int size = numResultsPerPage ?? rows.Count;
        HomeScreenSectionInfo[] slice = rows.Skip(Math.Max(page - 1, 0) * size).Take(size).ToArray();

        for (int i = 0; i < slice.Length; i++)
        {
            slice[i].OrderIndex = i;
        }

        return new QueryResult<HomeScreenSectionInfo>(0, slice.Length, slice);
    }

    private List<HomeScreenSectionInfo> Rows(User user, Guid view, Guid pageHash)
    {
        BaseItem? item = m_libraryManager.GetItemById(view);
        if (item == null || !item.IsVisible(user))
        {
            return new List<HomeScreenSectionInfo>();
        }

        List<IHomeScreenSection> sections = m_homeScreenManager.GetSectionTypes().ToList();
        EncoreCollectionRowSection? collectionRow = sections.OfType<EncoreCollectionRowSection>().FirstOrDefault();

        if (item is CollectionFolder { CollectionType: CollectionType.boxsets })
        {
            return VisibleCollections(user)
                .OrderBy(x => x.SortName)
                .Where(_ => collectionRow != null)
                .Select(x => WithRoute(collectionRow!.Row(x.Id, x.Name)))
                .ToList();
        }

        IReadOnlySet<Guid> scope = m_catalog.Scope(view);
        List<HomeScreenSectionInfo> fixedRows = new List<HomeScreenSectionInfo>();
        fixedRows.AddRange(Info<EncoreViewContinueSection>(sections, SectionViewMode.Landscape));
        fixedRows.AddRange(Info<EncoreViewNextUpSection>(sections, SectionViewMode.Landscape));
        fixedRows.AddRange(Info<EncoreMyListSection>(sections, null));
        if (item is BoxSet && collectionRow != null)
        {
            fixedRows.Add(collectionRow.Row(view, "Em destaque"));
        }

        List<HomeScreenSectionInfo> drawnRows = new List<HomeScreenSectionInfo>();
        drawnRows.AddRange(Info<EncoreForYouSection>(sections, null));
        drawnRows.AddRange(Info<EncoreWatchAgainSection>(sections, null));
        drawnRows.AddRange(sections.OfType<EncoreCatalogRowSection>().SelectMany(x => x.ViewRows(scope)));

        // Same as the home screen: a new order every time the page opens, kept while it loads more rows.
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{pageHash:N}|{view:N}"));
        Random random = new Random(BitConverter.ToInt32(hash, 0));
        return fixedRows.Concat(drawnRows.OrderBy(_ => random.Next())).ToList();
    }

    private static HomeScreenSectionInfo WithRoute(HomeScreenSectionInfo info)
    {
        info.Route = "encore-collection"; // the client makes the link, and only draws one when there is a route
        return info;
    }

    private static IEnumerable<HomeScreenSectionInfo> Info<T>(IEnumerable<IHomeScreenSection> sections, SectionViewMode? viewMode)
        where T : EncoreSectionBase
    {
        foreach (T section in sections.OfType<T>())
        {
            HomeScreenSectionInfo info = section.GetInfo();
            info.ViewMode = viewMode ?? info.ViewMode;
            yield return info;
        }
    }

    private IEnumerable<BaseItem> VisibleCollections(User user)
    {
        return m_libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.BoxSet },
            Recursive = true,
            DtoOptions = new DtoOptions(false)
        });
    }
}
