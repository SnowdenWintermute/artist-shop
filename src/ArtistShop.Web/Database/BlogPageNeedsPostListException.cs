namespace ArtistShop.Web.Database;

// Thrown by a repository when a change would leave the blog page without a post list, so a
// post's Up link would lead nowhere to read posts. The page says why.
public class BlogPageNeedsPostListException(string message, Exception innerException)
    : Exception(message, innerException);
