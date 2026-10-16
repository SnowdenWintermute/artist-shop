using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Publishing;

namespace ArtistShop.Web.Tests.Publishing;

public sealed class LibraryImageFieldsTests
{
    private const string KeptKey = "0123456789abcdef0123456789abcdef";
    private const string DeletedKey = "fedcba9876543210fedcba9876543210";

    private static readonly UploadedImage Kept = new(
        new UploadedImageId(1),
        KeptKey,
        "kept.jpg",
        800,
        600,
        "data:image/webp;base64,AAAA",
        "A pier",
        DateTimeOffset.UnixEpoch
    );

    private static string ImageOp(string storageKey) =>
        """{"insert":{"artshop-image":{"storageKey":"KEY","size":"small"}}}""".Replace("KEY", storageKey);

    private static RichText Body(params string[] storageKeys) =>
        new($$"""{"ops":[{{string.Join(",", storageKeys.Select(ImageOp))}},{"insert":"\n"}]}""");

    [Fact]
    public void FillsEachImageFromTheLibraryAndLeavesOutOneThatIsGone()
    {
        var filled = LibraryImageFields.Fill(Body(KeptKey, DeletedKey), new Dictionary<string, UploadedImage> { [KeptKey] = Kept });

        Assert.Equal(
            $$$$"""{"ops":[{"insert":{"artshop-image":{"storageKey":"{{{{KeptKey}}}}","size":"small","width":{{{{Kept.Width}}}},"height":{{{{Kept.Height}}}},"blur":"{{{{Kept.BlurDataUri}}}}","alt":"{{{{Kept.AltText}}}}"}}},{"insert":"\n"}]}""",
            filled.Json
        );
    }

    [Fact]
    public void StrippingWhatWasFilledGivesTheTextBack()
    {
        var body = Body(KeptKey);

        var stripped = LibraryImageFields.Strip(LibraryImageFields.Fill(body, new Dictionary<string, UploadedImage> { [KeptKey] = Kept }));

        Assert.Equal(body.Json, stripped.Json);
    }

    [Fact]
    public void FindsEachImageOnce()
    {
        Assert.Equal([KeptKey, DeletedKey], RichTextImages.UploadedImageKeysOf([RichTextParser.Parse(Body(KeptKey, DeletedKey, KeptKey))]));
    }
}
