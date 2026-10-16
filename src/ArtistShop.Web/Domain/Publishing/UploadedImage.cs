namespace ArtistShop.Web.Domain.Publishing;

public record UploadedImageId(int Value);

// An image in the library. AltText is empty when the artist cleared it
public record UploadedImage(
    UploadedImageId Id,
    string StorageKey,
    string OriginalFileName,
    int Width,
    int Height,
    string BlurDataUri,
    string AltText,
    DateTimeOffset UploadedAt
);

// UsedInCount is how many posts and text components show it
public record UploadedImageListItem(UploadedImage Image, int UsedInCount);

public record UploadedImageListPage(IReadOnlyList<UploadedImageListItem> Items, int TotalCount, int PageNumber, int PageSize)
{
    public int PageCount => Paging.PageCountOf(TotalCount, PageSize);
}

public enum UploadedImageUseType
{
    Post,
    Component,
}

// a post or text component showing a library image. PageNames are the pages a component is on
public record UploadedImageUse(UploadedImageUseType Type, string Name, IReadOnlyList<string> PageNames);
