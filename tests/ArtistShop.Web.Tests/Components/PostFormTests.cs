using ArtistShop.Web.Components.Pages.Admin.Publishing.Posts;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Tests.Components;

public sealed class PostFormTests : IDisposable
{
    private const string KeptKey = "0123456789abcdef0123456789abcdef";
    private const string SweptKey = "fedcba9876543210fedcba9876543210";

    private readonly DirectoryInfo _storageRoot;
    private readonly ImageStorage _imageStorage;

    public PostFormTests()
    {
        _storageRoot = Directory.CreateTempSubdirectory("artist-shop-tests-");
        _imageStorage = new ImageStorage(_storageRoot.FullName);
        Directory.CreateDirectory(_imageStorage.Originals);
        File.WriteAllText(_imageStorage.OriginalPath(KeptKey), "an upload");
    }

    public void Dispose() => _storageRoot.Delete(recursive: true);

    private static string ImageOp(string storageKey) =>
        """{"insert":{"artshop-image":{"storageKey":"KEY","width":800,"height":600,"alt":"KEY"}}}""".Replace("KEY", storageKey);

    [Fact]
    public void FindsTheUploadedImagesWhoseFilesTheSweepRemoved()
    {
        var form = new PostForm
        {
            Title = "Missing images",
            Body = $$"""{"ops":[{{ImageOp(KeptKey)}},{{ImageOp(SweptKey)}},{"insert":"\n"}]}""",
        };

        var missing = form.ImagesMissingFrom(_imageStorage);

        Assert.Equal(SweptKey, Assert.Single(missing).StorageKey);
    }
}
