namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;

// A custom list's card as its settings page lists it. Flat, since an island's parameters travel as JSON, which
// can't rebuild a GridCard from its abstract type
public record ChosenCardListItem(ChosenCardId Id, bool IsCollection, string Name, string EditHref, WorkImage? Image, bool IsVisible)
{
    public static ChosenCardListItem From(ChosenCard card) =>
        card.Card switch
        {
            GridCard.OfCollection collection => new(card.Id, IsCollection: true, collection.Collection.Name.Value,
                PageUrls.EditCollection(collection.Collection.Id), collection.Collection.Cover, card.IsVisible),
            GridCard.OfWork work => new(card.Id, IsCollection: false, work.Name.Value, PageUrls.EditWork(work.Id), work.Image,
                card.IsVisible),
            _ => throw new InvalidOperationException($"No list item for a {card.Card.GetType().Name}."),
        };
}
