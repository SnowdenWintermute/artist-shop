namespace ArtistShop.Web.Database;

// Thrown by a repository when a procedure finds the rows no longer match what the page loaded,
// for example another tab deleted the series. The page shows a message and reloads.
public class CatalogChangedException(string message, Exception innerException)
    : Exception(message, innerException);
