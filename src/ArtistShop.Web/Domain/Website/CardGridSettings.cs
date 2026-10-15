namespace ArtistShop.Web.Domain.Website;

using ArtistShop.Web.Domain.Catalog;

// The ids are only for ordering; settings store the names, and get_custom_pages_mentioning_work reads ChosenCards
public enum CardGridSourceType
{
    AllCollections = 1,
    WorksInCollection = 2,
    CollectionsAndLooseWorks = 3,
    ChosenCards = 4,
}

// which cards a grid shows, all of them ones a visitor may see
public abstract record CardGridSource
{
    public abstract CardGridSourceType Type { get; }

    // collectionId is only for WorksInCollection, which needs it
    public static CardGridSource Of(CardGridSourceType type, CollectionId? collectionId) =>
        type switch
        {
            CardGridSourceType.AllCollections => new AllCollections(),
            CardGridSourceType.WorksInCollection => new WorksInCollection(
                collectionId ?? throw new InvalidOperationException("Works in a collection need the collection.")
            ),
            CardGridSourceType.CollectionsAndLooseWorks => new CollectionsAndLooseWorks(),
            CardGridSourceType.ChosenCards => new ChosenCards(),
            _ => throw new InvalidOperationException($"{type} isn't a card grid source."),
        };

    // in the artist's order
    public sealed record AllCollections : CardGridSource
    {
        public override CardGridSourceType Type => CardGridSourceType.AllCollections;
    }

    // in the artist's order inside the collection
    public sealed record WorksInCollection(CollectionId CollectionId) : CardGridSource
    {
        public override CardGridSourceType Type => CardGridSourceType.WorksInCollection;
    }

    // the collections, then the works in none of them
    public sealed record CollectionsAndLooseWorks : CardGridSource
    {
        public override CardGridSourceType Type => CardGridSourceType.CollectionsAndLooseWorks;
    }

    // the artist's custom list, any mix of collections and works in their order, kept in card_grid_chosen_cards
    public sealed record ChosenCards : CardGridSource
    {
        public override CardGridSourceType Type => CardGridSourceType.ChosenCards;
    }
}

public enum CardGridHeadingType
{
    None = 1,
    // the collections word for all collections, the collection's name for one collection's works, the works
    // word for the mix. Not offered for a custom list
    Automatic = 2,
    Custom = 3,
}

public abstract record CardGridHeading
{
    public abstract CardGridHeadingType Type { get; }

    // text is only for Custom, which needs it
    public static CardGridHeading Of(CardGridHeadingType type, string? text) =>
        type switch
        {
            CardGridHeadingType.None => new None(),
            CardGridHeadingType.Automatic => new Automatic(),
            CardGridHeadingType.Custom => new Custom(text ?? throw new InvalidOperationException("A custom heading needs its text.")),
            _ => throw new InvalidOperationException($"{type} isn't a card grid heading."),
        };

    public sealed record None : CardGridHeading
    {
        public override CardGridHeadingType Type => CardGridHeadingType.None;
    }

    public sealed record Automatic : CardGridHeading
    {
        public override CardGridHeadingType Type => CardGridHeadingType.Automatic;
    }

    public sealed record Custom(string Text) : CardGridHeading
    {
        public override CardGridHeadingType Type => CardGridHeadingType.Custom;
    }
}

// how a grid's cards sit. Component settings store the names; collection_page stores the ids
public enum CardGridLayout
{
    // same-size cards with long titles cut short, and a short last page padded, so turning pages keeps one height
    EvenGrid = 1,
    // pictures in their own shapes, cards in rows as tall as their tallest
    Rows = 2,
    // pictures in their own shapes, packed into columns in the artist's order, left to right
    Masonry = 3,
}

// Without page links the grid shows only its first page, as a sample
public sealed record CardGridSettings(
    CardGridSource Source,
    CardGridHeading Heading,
    int CardsPerPage,
    bool HasPageLinks,
    CardGridLayout Layout
) : CustomComponentSettings
{
    public override CustomComponentType Type => CustomComponentType.CardGrid;

    public bool HasSameSizeCards => Layout is CardGridLayout.EvenGrid;

    public const int DefaultCardsPerPage = 24;

    public static readonly CardGridSettings AllCollections = new(
        new CardGridSource.AllCollections(),
        new CardGridHeading.Automatic(),
        DefaultCardsPerPage,
        HasPageLinks: true,
        CardGridLayout.EvenGrid
    );
}
