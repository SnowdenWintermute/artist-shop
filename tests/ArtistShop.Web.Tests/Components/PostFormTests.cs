using ArtistShop.Web.Components.Pages.Admin.Publishing.Posts;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Images;
using ArtistShop.Web.Tests.Database;

namespace ArtistShop.Web.Tests.Components;

[Collection(DatabaseCollection.Name)]
public sealed class PostFormTests(TestDatabaseFixture database)
{
    private readonly UploadedImageRepository _library = new(database.Site);

    private static string ImageOp(string storageKey) =>
        """{"insert":{"artshop-image":{"storageKey":"KEY","width":800,"height":600,"alt":"KEY"}}}""".Replace("KEY", storageKey);

    [Fact]
    public async Task FindsTheImagesDeletedFromTheLibrary()
    {
        var keptKey = ImageStorage.NewStorageKey();
        await _library.AddAsync(new UploadedImageAddition(keptKey, "kept.jpg", new string('0', 64), 800, 600, "data:image/webp;base64,AAAA", "Kept"));
        var form = new PostForm
        {
            Title = "Missing images",
            Body = $$"""{"ops":[{{ImageOp(keptKey)}},{{ImageOp(ImageStorage.NewStorageKey())}},{"insert":"\n"}]}""",
        };

        Assert.False(await form.CheckBodyAsync(_library));

        Assert.Single(form.EditContext.GetValidationMessages(form.EditContext.Field(nameof(PostForm.Body))));
    }

    // the editor's copy of the library's fields isn't kept with the post
    [Fact]
    public void SavesTheBodyWithoutTheLibrarysFields()
    {
        var storageKey = ImageStorage.NewStorageKey();
        var form = new PostForm { Title = "Saved", Body = $$"""{"ops":[{{ImageOp(storageKey)}},{"insert":"\n"}]}""" };

        Assert.Equal(
            $$$$"""{"ops":[{"insert":{"artshop-image":{"storageKey":"{{{{storageKey}}}}"}}},{"insert":"\n"}]}""",
            form.ToPostBody().Json
        );
    }
}
