using Jellyfin.Plugin.HomeScreenSections.Configuration;
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

/// <summary>What the Encore rows share: reading the items through the user's permissions, in the order given.</summary>
public abstract class EncoreSectionBase : IHomeScreenSection
{
    protected const int RowSize = 16;

    private static readonly DtoOptions s_dtoOptions = new DtoOptions
    {
        Fields = new[] { ItemFields.PrimaryImageAspectRatio, ItemFields.MediaSourceCount }
    };

    protected EncoreSectionBase(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService)
    {
        UserManager = userManager;
        LibraryManager = libraryManager;
        DtoService = dtoService;
    }

    public abstract string? Section { get; }

    public string? DisplayText { get; set; }

    public virtual string? AdminDescription => null;

    public virtual int? Limit => 1;

    public string? Route => null;

    public string? AdditionalData { get; set; }

    public object? OriginalPayload => null;

    protected IUserManager UserManager { get; }

    protected ILibraryManager LibraryManager { get; }

    protected IDtoService DtoService { get; }

    public abstract QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload, IQueryCollection queryCollection);

    public virtual IEnumerable<IHomeScreenSection> CreateInstances(Guid? userId, int instanceCount)
    {
        yield return this;
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

    protected QueryResult<BaseItemDto> ReadItems(User user, IEnumerable<Guid> ids)
    {
        // Asks for more than it shows because some may be hidden from this user.
        Guid[] wanted = ids.Take(RowSize + 8).ToArray();
        if (wanted.Length == 0)
        {
            return new QueryResult<BaseItemDto>();
        }

        Dictionary<Guid, BaseItem> found = LibraryManager.GetItemList(new InternalItemsQuery(user)
        {
            ItemIds = wanted,
            DtoOptions = s_dtoOptions
        }).ToDictionary(x => x.Id);

        BaseItem[] items = wanted.Where(found.ContainsKey).Select(x => found[x]).Take(RowSize).ToArray();
        return new QueryResult<BaseItemDto>(DtoService.GetBaseItemDtos(items, s_dtoOptions, user));
    }
}
