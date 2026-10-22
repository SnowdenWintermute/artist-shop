namespace ArtistShop.Web.Database;

// Thrown by a repository when a change would leave the collections page without a card grid of all
// collections, so a collection's Up link would lead nowhere to see the others. The page says why.
public class CollectionsPageNeedsGridException(string message, Exception innerException)
    : Exception(message, innerException);
