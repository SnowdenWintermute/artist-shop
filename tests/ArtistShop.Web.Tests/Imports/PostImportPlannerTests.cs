using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Images;
using ArtistShop.Web.Imports;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Tests.Imports;

public class PostImportPlannerTests
{
    private const string DawnKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string DawnSha256 = "d0d0";

    private static readonly Artwork Dawn = new(
        new ArtworkId(7),
        new ArtworkType(new ArtworkTypeId(1), new ArtworkTypeName("Painting")),
        new ArtworkName("Dawn"),
        new ArtworkSlug("dawn"),
        description: null,
        dateCreated: null,
        dimensions: null,
        duration: null,
        [new ArtworkImage(DawnKey, OriginalFileName: null, Width: 800, Height: 600, BlurDataUri: null)],
        series: [],
        vocabularyTerms: [],
        products: []
    );

    private static PostImportTarget Target(IReadOnlyList<Artwork>? artworks = null, IReadOnlySet<string>? postSlugs = null) =>
        new(
            postSlugs ?? new HashSet<string>(),
            new HashSet<string> { "gardens" },
            artworks ?? [Dawn],
            new Dictionary<string, string> { [DawnKey] = DawnSha256 }
        );

    private static string PostJson(string title, string ops, string publishedAt = "\"2025-03-04T05:06:07Z\"") =>
        $$$"""{"formatVersion":1,"title":"{{{title}}}","publishedAt":{{{publishedAt}}},"body":{"ops":[{{{ops}}},{"insert":"\n"}]}}""";

    private static PostImportFolder Folder(string name, string json, params string[] files) =>
        new(name, json, files.ToDictionary(file => file, file => $"id-{file}"));

    private static PostImportItem PlanOne(PostImportFolder folder, PostImportTarget? target = null) =>
        Assert.Single(PostImportPlanner.Plan([folder], target ?? Target()));

    private static string ArtworkOp(string slug, string title, string sha256) =>
        $$$$"""{"insert":{"artshop-artwork":{"artworkSlug":"{{{{slug}}}}","artworkTitle":"{{{{title}}}}","imageSha256":"{{{{sha256}}}}","file":"image-1.webp","size":"small","layout":"floatLeft","caption":"Early"}}}""";

    [Fact]
    public void AnArtworkWithTheSameImageIsLinkedAgain()
    {
        var item = PlanOne(Folder("spring", PostJson("Spring", ArtworkOp("dawn", "Dawn", DawnSha256)), "image-1.webp"));

        Assert.Equal(PostImportOutcome.WillAdd, item.Outcome);
        Assert.Equal(1, item.RelinkedArtworkCount);
        var embed = Assert.Single(DocumentOf(item).Blocks.OfType<ArtworkEmbedBlock>());
        Assert.Equal(new ArtworkEmbedBlock(Dawn.Id, DawnKey, EmbedImageSize.Small, EmbedLayout.FloatLeft, "Early"), embed);
        // its web copy isn't needed, so nothing is uploaded
        Assert.Empty(item.FileIdsByPlaceholder);
    }

    // the catalog import makes a slug from the title, so one taken here gives the artwork another
    [Fact]
    public void AnArtworkWhoseSlugChangedIsFoundByItsTitle()
    {
        var item = PlanOne(Folder("spring", PostJson("Spring", ArtworkOp("dawn-2", "Dawn", DawnSha256)), "image-1.webp"));

        Assert.Equal(1, item.RelinkedArtworkCount);
    }

    [Fact]
    public void AnArtworkWithoutTheSameImageBecomesAPictureOfItsWebCopy()
    {
        var item = PlanOne(Folder("spring", PostJson("Spring", ArtworkOp("dawn", "Dawn", "another")), "image-1.webp"));

        Assert.Equal(PostImportOutcome.WillAdd, item.Outcome);
        Assert.Equal(["Dawn"], item.PictureOnlyArtworks);
        var image = Assert.Single(DocumentOf(item).Blocks.OfType<PostImageEmbedBlock>());
        Assert.Equal("Dawn", image.Alt);
        Assert.Equal("Early", image.Caption);
        Assert.Equal(EmbedLayout.FloatLeft, image.Layout);
        Assert.Equal("id-image-1.webp", item.FileIdsByPlaceholder[image.StorageKey]);
    }

    // a key in the file could name another image on this website, so only the file counts
    [Fact]
    public void AStorageKeyInTheFileIsNeverUsed()
    {
        var ops = $$$$"""{"insert":{"artshop-image":{"storageKey":"{{{{DawnKey}}}}","width":10,"height":10,"alt":"Stolen"}}}""";

        var item = PlanOne(Folder("spring", PostJson("Spring", ops)));

        Assert.Equal(PostImportOutcome.Refused, item.Outcome);
        Assert.Equal(["An image doesn't say which file it is."], item.Problems);
    }

    [Fact]
    public void AnImageShownTwiceIsUploadedOnce()
    {
        const string image = """{"insert":{"artshop-image":{"file":"image-1-original.jpg","width":10,"height":10,"alt":"Pencil","lightbox":true}}}""";

        var item = PlanOne(Folder("spring", PostJson("Spring", $"{image},{image}"), "image-1-original.jpg"));

        var embeds = DocumentOf(item).Blocks.OfType<PostImageEmbedBlock>().ToList();
        Assert.Equal(2, embeds.Count);
        Assert.Equal(embeds[0].StorageKey, embeds[1].StorageKey);
        Assert.True(embeds[0].OpensLightbox);
        Assert.Equal(["id-image-1-original.jpg"], item.FileIdsByPlaceholder.Values);
    }

    [Fact]
    public void AnImageWhoseFileIsMissingRefusesThePost()
    {
        const string image = """{"insert":{"artshop-image":{"file":"image-1-original.jpg","width":10,"height":10,"alt":""}}}""";

        var item = PlanOne(Folder("spring", PostJson("Spring", image)));

        Assert.Equal(PostImportOutcome.Refused, item.Outcome);
        Assert.Equal(["The image image-1-original.jpg isn't in its folder."], item.Problems);
    }

    [Fact]
    public void ALinkASaveWouldRefuseRefusesThePost()
    {
        var item = PlanOne(Folder("spring", PostJson("Spring", """{"insert":"click","attributes":{"link":"javascript:alert(1)"}}""")));

        Assert.Equal(PostImportOutcome.Refused, item.Outcome);
        Assert.Equal([PostDocumentParser.DroppedLinkProblem("javascript:alert(1)")], item.Problems);
    }

    [Fact]
    public void ALinkToAPageThatIsntHereIsAWarning()
    {
        string[] paths = ["/series/gardens", "/series/lost", "/artworks/dawn", "/posts/summer", "/posts/gone", "/about"];
        var links = string.Join(
            ",",
            paths.Select(link =>
                $$$"""{"insert":"x","attributes":{"link":"{{{link}}}"}}"""
            )
        );

        var items = PostImportPlanner.Plan(
            [Folder("spring", PostJson("Spring", links)), Folder("summer", PostJson("Summer", """{"insert":"Hot"}"""))],
            Target()
        );

        var spring = items.Single(item => item.FolderName == "spring");
        Assert.Equal(PostImportOutcome.WillAdd, spring.Outcome);
        // /posts/summer works, since that post comes in with it
        Assert.Equal(["/series/lost", "/posts/gone", "/about"], spring.BrokenLinks);
    }

    [Fact]
    public void APostWhoseTitleIsTakenIsSkipped()
    {
        var items = PostImportPlanner.Plan(
            [
                Folder("a", PostJson("Spring", """{"insert":"Hi"}""")),
                Folder("b", PostJson("Summer", """{"insert":"Hi"}""")),
                Folder("c", PostJson("summer!", """{"insert":"Hi"}""")),
            ],
            Target(postSlugs: new HashSet<string> { "spring" })
        );

        Assert.Equal([PostImportOutcome.Skipped, PostImportOutcome.WillAdd, PostImportOutcome.Skipped], items.Select(item => item.Outcome));
    }

    [Fact]
    public void ADraftStaysADraftAndAPublishedPostKeepsItsDate()
    {
        var items = PostImportPlanner.Plan(
            [
                Folder("a", PostJson("Published", """{"insert":"Hi"}""")),
                Folder("b", PostJson("Draft", """{"insert":"Hi"}""", publishedAt: "null")),
            ],
            Target()
        );

        Assert.Equal(new DateTimeOffset(2025, 3, 4, 5, 6, 7, TimeSpan.Zero), items[0].PublishedAt);
        Assert.Null(items[1].PublishedAt);
    }

    [Fact]
    public void AFileFromANewerVersionIsRefused()
    {
        var item = PlanOne(Folder("spring", """{"formatVersion":2,"title":"Spring","publishedAt":null,"body":{"ops":[]}}"""));

        Assert.Equal(PostImportOutcome.Refused, item.Outcome);
        Assert.Contains("newer version", Assert.Single(item.Problems));
    }

    [Fact]
    public void OnlyTheImagesOfArtworksAPostMayShowAreHashed()
    {
        var other = new Artwork(
            new ArtworkId(8),
            Dawn.Type,
            new ArtworkName("Dusk"),
            new ArtworkSlug("dusk"),
            description: null,
            dateCreated: null,
            dimensions: null,
            duration: null,
            [new ArtworkImage("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", OriginalFileName: null, Width: 800, Height: 600, BlurDataUri: null)],
            series: [],
            vocabularyTerms: [],
            products: []
        );

        var keys = PostImportPlanner.ArtworkImagesToHash([Folder("spring", PostJson("Spring", ArtworkOp("dawn", "Dawn", DawnSha256)))], [Dawn, other]);

        Assert.Equal([DawnKey], keys);
    }

    [Fact]
    public void FinishingSwapsEachPlaceholderForItsUpload()
    {
        const string image = """{"insert":{"artshop-image":{"file":"image-1-original.jpg","width":10,"height":10,"alt":"Pencil"}}}""";
        var item = PlanOne(Folder("spring", PostJson("Spring", image), "image-1-original.jpg"));
        var placeholder = Assert.Single(item.FileIdsByPlaceholder.Keys);
        var upload = new ImageUploadResult("cccccccccccccccccccccccccccccccc", "x.jpg", 1200, 900, "data:image/webp;base64,AAAA");

        var body = PostImportPlanner.Finish(DocumentOf(item), new Dictionary<string, ImageUploadResult> { [placeholder] = upload });

        var saved = Assert.Single(PostDocumentParser.Parse(body).Blocks.OfType<PostImageEmbedBlock>());
        Assert.Equal(("cccccccccccccccccccccccccccccccc", 1200, 900, "data:image/webp;base64,AAAA", "Pencil"), (saved.StorageKey, saved.Width, saved.Height, saved.BlurDataUri, saved.Alt));
    }

    private static PostDocument DocumentOf(PostImportItem item) => Unwrap.Value(item.Document);
}
