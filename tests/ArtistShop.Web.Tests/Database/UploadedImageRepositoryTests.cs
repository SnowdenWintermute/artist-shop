using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Images;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Tests.Database;

// The test database is shared, so each test searches for a word only its own images have
[Collection(DatabaseCollection.Name)]
public sealed class UploadedImageRepositoryTests(TestDatabaseFixture database)
{
    private readonly UploadedImageRepository _library = new(database.Site);

    private static string NewWord() => $"w{Guid.NewGuid():n}";

    private async Task<string> AddAsync(string originalFileName, string altText)
    {
        var storageKey = ImageStorage.NewStorageKey();
        await _library.AddAsync(
            new UploadedImageAddition(storageKey, originalFileName, new string('0', 64), 800, 600, "data:image/webp;base64,AAAA", Unwrap.Value(AltText.Read(altText)))
        );
        return storageKey;
    }

    private static RichText BodyShowing(string storageKey) =>
        new("""{"ops":[{"insert":{"artshop-image":{"storageKey":"KEY"}}},{"insert":{"artshop-image":{"storageKey":"KEY"}}},{"insert":"\n"}]}""".Replace("KEY", storageKey));

    [Fact]
    public async Task ASearchMatchesAltTextOrFileNameNewestFirst()
    {
        var word = NewWord();
        var byAlt = await AddAsync("pier.jpg", $"Café {word}");
        var byFileName = await AddAsync($"{word}-dusk.png", "Dusk");
        await AddAsync("other.jpg", "Other");

        var page = await _library.GetPageAsync(word, pageNumber: 1);

        Assert.Equal([byFileName, byAlt], page.Items.Select(item => item.Image.StorageKey));
        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task ASearchIgnoresCaseAndAccents()
    {
        var word = NewWord();
        var storageKey = await AddAsync("pier.jpg", $"Café {word}");

        var page = await _library.GetPageAsync($"CAFE {word.ToUpperInvariant()}", pageNumber: 1);

        Assert.Equal([storageKey], page.Items.Select(item => item.Image.StorageKey));
    }

    // a post and a text component on a page, each showing the image
    private async Task<(PostId PostId, CustomComponentId ComponentId)> ShowInAPostAndAComponentAsync(string word, string storageKey)
    {
        var postId = await new PostRepository(database.Site).AddAsync(new PostTitle(word), PostSlug.FromTitle(word), BodyShowing(storageKey), PostStatus.Draft);
        var pages = new CustomPageRepository(database.Site);
        var pageId = await pages.AddAsync(word, new CustomPageAddress(word));
        await new PageRowRepository(database.Site).AddAsync(pageId);
        var slot = Assert.Single(Assert.Single((await pages.GetLayoutAsync(pageId)).Rows).Slots);
        var componentId = await new CustomComponentRepository(database.Site).AddToSlotAsync(
            slot.Id,
            word,
            new TextSettings(BodyShowing(storageKey), TextWidth.Narrow)
        );
        return (postId, componentId);
    }

    // a post showing it twice is one place
    [Fact]
    public async Task CountsThePostsAndTextComponentsShowingAnImage()
    {
        var word = NewWord();
        var storageKey = await AddAsync("pier.jpg", word);
        await ShowInAPostAndAComponentAsync(word, storageKey);

        var item = Assert.Single((await _library.GetPageAsync(word, pageNumber: 1)).Items);

        Assert.Equal(2, item.UsedInCount);
    }

    [Fact]
    public async Task APostSavedWithoutTheImageStopsCounting()
    {
        var word = NewWord();
        var storageKey = await AddAsync("pier.jpg", word);
        var (postId, _) = await ShowInAPostAndAComponentAsync(word, storageKey);

        await new PostRepository(database.Site).UpdateAsync(
            postId,
            new PostTitle(word),
            PostSlug.FromTitle(word),
            new RichText("""{"ops":[{"insert":"\n"}]}"""),
            PostStatus.Draft
        );

        var item = Assert.Single((await _library.GetPageAsync(word, pageNumber: 1)).Items);
        Assert.Equal(1, item.UsedInCount);
    }

    [Fact]
    public async Task ListsWhereImagesAreShownWithAComponentsPages()
    {
        var word = NewWord();
        var storageKey = await AddAsync("pier.jpg", word);
        await ShowInAPostAndAComponentAsync(word, storageKey);

        var uses = await _library.GetUsesAsync([storageKey]);

        Assert.Equivalent(
            new[]
            {
                new UploadedImageUse(UploadedImageUseType.Post, word, []),
                new UploadedImageUse(UploadedImageUseType.Component, word, [word]),
            },
            uses,
            strict: true
        );
    }

    // its embeds go with it, and the post and component keep their last edited time
    [Fact]
    public async Task DeletingAnImageTakesItsEmbedsOutOfPostsAndComponents()
    {
        var word = NewWord();
        var storageKey = await AddAsync("pier.jpg", word);
        var kept = await AddAsync("kept.jpg", word);
        var (postId, componentId) = await ShowInAPostAndAComponentAsync(word, storageKey);
        var posts = new PostRepository(database.Site);
        var updatedAt = (await posts.GetAsync(postId))?.UpdatedAt;

        await _library.DeleteAsync([storageKey]);

        Assert.Equal([kept], (await _library.GetPageAsync(word, pageNumber: 1)).Items.Select(item => item.Image.StorageKey));
        var post = await posts.GetAsync(postId);
        Assert.NotNull(post);
        Assert.Empty(RichTextParser.Parse(post.Body).Blocks.OfType<UploadedImageEmbedBlock>());
        Assert.Equal(updatedAt, post.UpdatedAt);
        var component = await new CustomComponentRepository(database.Site).GetAsync(componentId);
        var text = Assert.IsType<TextSettings>(component?.Component.Settings);
        Assert.Empty(RichTextParser.Parse(text.Body).Blocks.OfType<UploadedImageEmbedBlock>());
        Assert.Empty(await _library.GetUsesAsync([storageKey]));
    }

    // an image component on its own page, showing the image
    private async Task<CustomComponentId> ShowInAnImageComponentAsync(string word, string storageKey)
    {
        var pages = new CustomPageRepository(database.Site);
        var pageId = await pages.AddAsync($"{word} image", new CustomPageAddress($"{word}-image"));
        await new PageRowRepository(database.Site).AddAsync(pageId);
        var slot = Assert.Single(Assert.Single((await pages.GetLayoutAsync(pageId)).Rows).Slots);
        return await new CustomComponentRepository(database.Site).AddToSlotAsync(
            slot.Id,
            $"{word} image",
            ImageSettings.New with { Source = new ImageSource.Library(storageKey) }
        );
    }

    // counted in its uses, but listed apart from them, as deleting the image deletes the component
    [Fact]
    public async Task AnImageComponentShowingAnImageIsListedApartFromItsUses()
    {
        var word = NewWord();
        var storageKey = await AddAsync("pier.jpg", word);
        var componentId = await ShowInAnImageComponentAsync(word, storageKey);

        Assert.Equal(1, Assert.Single((await _library.GetPageAsync(word, pageNumber: 1)).Items).UsedInCount);
        Assert.Empty(await _library.GetUsesAsync([storageKey]));
        Assert.Equivalent(
            new[] { new CustomComponentOnPages(componentId, $"{word} image", [$"{word} image"]) },
            await new CustomComponentRepository(database.Site).GetImageComponentsShowingImagesAsync([storageKey]),
            strict: true
        );
    }

    // gone from its page too, beside a text component losing its embed in the same delete
    [Fact]
    public async Task DeletingAnImageDeletesTheImageComponentsShowingIt()
    {
        var word = NewWord();
        var storageKey = await AddAsync("pier.jpg", word);
        var (_, textComponentId) = await ShowInAPostAndAComponentAsync(word, storageKey);
        var componentId = await ShowInAnImageComponentAsync(word, storageKey);
        var components = new CustomComponentRepository(database.Site);

        await _library.DeleteAsync([storageKey]);

        Assert.Null(await components.GetAsync(componentId));
        Assert.NotNull(await components.GetAsync(textComponentId));
    }
}
