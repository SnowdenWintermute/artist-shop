namespace ArtistShop.Web.Database;

// Thrown when a procedure finds the work a page is working on is gone. Separate from
// ChangedSincePageLoadException because there is nothing to correct and submit again: the page can only
// say so and offer a way back.
public class WorkDeletedException(string message, Exception innerException)
    : Exception(message, innerException);
