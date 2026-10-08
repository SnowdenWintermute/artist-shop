namespace ArtistShop.Web.Domain.Catalog;

// One image and the work it belongs to, for a page that shows a single image of a work
// rather than the work itself, such as an embed in a post. ImageNumber is its place on the
// work page, counted from one as that page's address does
public record WorkImageWithWork(
    WorkId WorkId,
    WorkLink Work,
    WorkImage Image,
    int ImageNumber
);
