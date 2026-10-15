namespace ArtistShop.Web.Domain.Website;

public record ChosenCardId(int Value);

// One card on a card grid's custom list. Its collection's cover or its work's image is null when a visitor
// wouldn't see it, so the settings page can say why it isn't shown
public record ChosenCard(ChosenCardId Id, GridCard Card)
{
    public bool IsVisible =>
        Card switch
        {
            GridCard.OfCollection collection => collection.Collection.Cover is not null,
            GridCard.OfWork work => work.Image is not null,
            _ => throw new InvalidOperationException($"No visibility rule for a {Card.GetType().Name}."),
        };
}
