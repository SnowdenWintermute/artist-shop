using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CollectionPage;

// form posts create this, so it keeps a single public constructor
public class CollectionPageForm : ServerValidatedForm
{
    [Range(1, ArtistShopLimits.CardGridMaximumCardsPerPage)]
    public int? CardsPerPage { get; set; }

    public CardGridLayout Layout { get; set; }

    public static CollectionPageForm From(CollectionPageSettings settings) =>
        new() { CardsPerPage = settings.CardsPerPage, Layout = settings.Layout };

    // what a post left out, as a script might, which would otherwise save 0. False when the form says by the
    // field why
    public bool Check()
    {
        var isValid = true;

        if (!Enum.IsDefined(Layout))
        {
            AddServerError(nameof(Layout), "Choose one.");
            isValid = false;
        }

        if (CardsPerPage is null)
        {
            AddServerError(nameof(CardsPerPage), "Say how many cards a page holds.");
            isValid = false;
        }

        return isValid;
    }

    public CollectionPageSettings ToSettings() => new(Layout, Unwrap.Value(CardsPerPage));
}
