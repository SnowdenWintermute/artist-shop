namespace ArtistShop.Web.Database;

// Thrown when a procedure finds the artwork a page is working on is gone. Separate from
// CatalogChangedException because there is nothing to correct and submit again: the page can only
// say so and offer a way back.
public class ArtworkDeletedException(string message, Exception innerException)
    : Exception(message, innerException);
