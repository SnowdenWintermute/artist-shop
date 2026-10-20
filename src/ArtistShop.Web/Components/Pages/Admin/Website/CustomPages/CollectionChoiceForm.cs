using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// A form holding a CollectionChoiceField, which sets CollectionId: the collection a card grid, carousel or
// slideshow shows the works of
public abstract class CollectionChoiceForm : ImagePickerForm
{
    public int? CollectionId { get; set; }

    protected CollectionId? ChosenCollectionId => CollectionId is int id ? new CollectionId(id) : null;

    // a saved source's collection as the field holds it
    protected static int? CollectionIdOf(CardGridSource source) =>
        source is CardGridSource.WorksInCollection inCollection ? inCollection.CollectionId.Value : null;

    protected static int? CollectionIdOf(SlideSource? source) =>
        source is SlideSource.WorksInCollection inCollection ? inCollection.CollectionId.Value : null;

    // False when the form says by the field why: none chosen, or one deleted since the page showed it
    protected bool CheckCollectionChosen(IReadOnlyList<Collection> collections)
    {
        if (collections.Any(collection => collection.Id.Value == CollectionId))
        {
            return true;
        }

        AddServerError(nameof(CollectionId), "Choose one.");
        return false;
    }
}
