using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;
using Microsoft.AspNetCore.Components;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// The pages adding works or collections to a card grid's custom list. The grid, the row editor its settings page
// was opened from and the checked ids all come from the address
public abstract class AddToCardGridPage : ComponentBase
{
    protected const string MissingMessage = "This card grid doesn't exist. It may have been deleted.";

    [Inject]
    public required CustomComponentRepository CustomComponentRepository { get; set; }
    [Inject]
    public required NavigationManager NavigationManager { get; set; }

    [CascadingParameter]
    public required SiteWording Wording { get; set; }

    [Parameter]
    public int Id { get; set; }

    [SupplyParameterFromQuery(Name = PageUrls.AddCheckedKey)]
    protected int[]? CheckedIds { get; set; }
    [SupplyParameterFromQuery(Name = PageUrls.ComponentPageKey)]
    protected int? FromPageId { get; set; }
    [SupplyParameterFromQuery(Name = PageUrls.ComponentRowKey)]
    protected int? FromRowId { get; set; }

    // null when it's gone, or the address names a component of another type
    protected CustomComponent? Grid { get; private set; }

    protected CustomComponentId ComponentId => new(Id);

    protected CustomComponentOrigin? Origin => CustomComponentOrigin.Of(FromPageId, FromRowId);

    // an id checked twice, in an address edited by hand, counts once
    protected IEnumerable<int> CheckedIdValues => (CheckedIds ?? []).Distinct();

    protected override async Task OnInitializedAsync()
    {
        Grid = await CustomComponentRepository.GetAsync(ComponentId) is { Component.Settings: CardGridSettings } found
            ? found.Component
            : null;

        if (Grid is not null)
        {
            await OnGridFoundAsync();
        }
    }

    protected virtual Task OnGridFoundAsync() => Task.CompletedTask;

    protected abstract Task AddCheckedAsync();

    // the form posts to the page's own address, so the checks arrive in it
    protected async Task AddAsync()
    {
        try
        {
            await AddCheckedAsync();
        }
        catch (ChangedSincePageLoadException)
        {
            // the grid was deleted elsewhere; its settings page says so
        }

        NavigationManager.NavigateTo(PageUrls.EditCustomComponent(ComponentId, Origin));
    }
}
