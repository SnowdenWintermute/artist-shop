using ArtistShop.Web.Domain;
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

    private static readonly Work Dawn = new(
        new WorkId(7),
        new WorkType(new WorkTypeId(1), new WorkTypeName("Painting")),
        new WorkName("Dawn"),
        new WorkSlug("dawn"),
        description: null,
        dateCreated: null,
        dimensions: null,
        duration: null,
        [new WorkImage(DawnKey, OriginalFileName: null, Width: 800, Height: 600, BlurDataUri: null)],
        collections: [],
        vocabularyTerms: [],
        products: []
    );

    private static PostImportTarget Target(IReadOnlyList<Work>? works = null, IReadOnlySet<string>? postSlugs = null) =>
        new(
            postSlugs ?? new HashSet<string>(),
            new HashSet<string> { "gardens" },
            new WebsiteWorks(works ?? [Dawn], new Dictionary<string, string> { [DawnKey] = DawnSha256 })
        );

    private static string PostJson(string title, string ops, string publishedAt = "\"2025-03-04T05:06:07Z\"") =>
        $$$$"""{"formatVersion":1,"title":"{{{{title}}}}","publishedAt":{{{{publishedAt}}}},"body":{"ops":[{{{{ops}}}},{"insert":"\n"}]}}""";

    private static PostImportFolder Folder(string name, string json, params string[] files) =>
        new(name, json, files.ToDictionary(file => file, file => $"id-{file}"));

    private static PostImportItem PlanOne(PostImportFolder folder, PostImportTarget? target = null) =>
        Assert.Single(PostImportPlanner.Plan([folder], target ?? Target()));

    private static string WorkOp(string slug, string title, string sha256) =>
        $$$$"""{"insert":{"artshop-work":{"workSlug":"{{{{slug}}}}","workTitle":"{{{{title}}}}","imageSha256":"{{{{sha256}}}}","file":"image-1.webp","size":"small","layout":"floatLeft","caption":"Early"}}}""";

    [Fact]
    public void AWorkWithTheSameImageIsLinkedAgain()
    {
        var item = PlanOne(Folder("spring", PostJson("Spring", WorkOp("dawn", "Dawn", DawnSha256)), "image-1.webp"));

        Assert.Equal(PostImportOutcome.WillAdd, item.Outcome);
        Assert.Equal(1, item.RelinkedWorkCount);
        var embed = Assert.Single(DocumentOf(item).Blocks.OfType<WorkEmbedBlock>());
        Assert.Equal(new WorkEmbedBlock(Dawn.Id, DawnKey, EmbedImageSize.Small, EmbedLayout.FloatLeft, "Early"), embed);
        // its web copy isn't needed, so nothing is uploaded
        Assert.Empty(item.ImagesByPlaceholder);
    }

    // the catalog import makes a slug from the title, so one taken here gives the work another
    [Fact]
    public void AWorkWhoseSlugChangedIsFoundByItsTitle()
    {
        var item = PlanOne(Folder("spring", PostJson("Spring", WorkOp("dawn-2", "Dawn", DawnSha256)), "image-1.webp"));

        Assert.Equal(1, item.RelinkedWorkCount);
    }

    [Fact]
    public void AWorkWithoutTheSameImageBecomesAPictureOfItsWebCopy()
    {
        var item = PlanOne(Folder("spring", PostJson("Spring", WorkOp("dawn", "Dawn", "another")), "image-1.webp"));

        Assert.Equal(PostImportOutcome.WillAdd, item.Outcome);
        Assert.Equal(["Dawn"], item.PictureOnlyWorks);
        var image = Assert.Single(DocumentOf(item).Blocks.OfType<UploadedImageEmbedBlock>());
        Assert.Equal("Early", image.Caption);
        Assert.Equal(EmbedLayout.FloatLeft, image.Layout);
        // the work's title is its alt text
        Assert.Equal(new PlannedPostImage("id-image-1.webp", Unwrap.Value(AltText.Read("Dawn"))), item.ImagesByPlaceholder[image.StorageKey]);
    }

    // a key in the file could name another image on this website, so only the file counts
    [Fact]
    public void AStorageKeyInTheFileIsNeverUsed()
    {
        var ops = $$$$"""{"insert":{"artshop-image":{"storageKey":"{{{{DawnKey}}}}","alt":"Stolen"}}}""";

        var item = PlanOne(Folder("spring", PostJson("Spring", ops)));

        Assert.Equal(PostImportOutcome.Refused, item.Outcome);
        Assert.Equal(["An image doesn't say which file it is."], item.Problems);
    }

    [Fact]
    public void AnImageShownTwiceIsUploadedOnce()
    {
        const string first = """{"insert":{"artshop-image":{"file":"image-1-original.jpg","alt":" Pencil ","lightbox":true}}}""";
        const string second = """{"insert":{"artshop-image":{"file":"image-1-original.jpg","alt":"Another"}}}""";

        var item = PlanOne(Folder("spring", PostJson("Spring", $"{first},{second}"), "image-1-original.jpg"));

        var embeds = DocumentOf(item).Blocks.OfType<UploadedImageEmbedBlock>().ToList();
        Assert.Equal(2, embeds.Count);
        Assert.Equal(embeds[0].StorageKey, embeds[1].StorageKey);
        Assert.True(embeds[0].OpensLightbox);
        // one library image, with the first one's alt text
        Assert.Equal([new PlannedPostImage("id-image-1-original.jpg", Unwrap.Value(AltText.Read("Pencil")))], item.ImagesByPlaceholder.Values);
    }

    [Fact]
    public void AnImageWhoseFileIsMissingRefusesThePost()
    {
        const string image = """{"insert":{"artshop-image":{"file":"image-1-original.jpg","alt":""}}}""";

        var item = PlanOne(Folder("spring", PostJson("Spring", image)));

        Assert.Equal(PostImportOutcome.Refused, item.Outcome);
        Assert.Equal(["The image image-1-original.jpg isn't in its folder."], item.Problems);
    }

    [Fact]
    public void ALinkASaveWouldRefuseRefusesThePost()
    {
        var item = PlanOne(Folder("spring", PostJson("Spring", """{"insert":"click","attributes":{"link":"javascript:alert(1)"}}""")));

        Assert.Equal(PostImportOutcome.Refused, item.Outcome);
        Assert.Equal([RichTextParser.DroppedLinkProblem("javascript:alert(1)")], item.Problems);
    }

    [Fact]
    public void ALinkToAPageThatIsntHereIsAWarning()
    {
        string[] paths = ["/collections/gardens", "/collections/lost", "/works/dawn", "/posts/summer", "/posts/gone", "/about"];
        var links = string.Join(
            ",",
            paths.Select(link =>
                $$$$"""{"insert":"x","attributes":{"link":"{{{{link}}}}"}}"""
            )
        );

        var items = PostImportPlanner.Plan(
            [Folder("spring", PostJson("Spring", links)), Folder("summer", PostJson("Summer", """{"insert":"Hot"}"""))],
            Target()
        );

        var spring = items.Single(item => item.FolderName == "spring");
        Assert.Equal(PostImportOutcome.WillAdd, spring.Outcome);
        // /posts/summer works, since that post comes in with it
        Assert.Equal(["/collections/lost", "/posts/gone", "/about"], spring.BrokenLinks);
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
    public void OnlyTheImagesOfWorksAPostMayShowAreHashed()
    {
        var other = new Work(
            new WorkId(8),
            Dawn.Type,
            new WorkName("Dusk"),
            new WorkSlug("dusk"),
            description: null,
            dateCreated: null,
            dimensions: null,
            duration: null,
            [new WorkImage("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", OriginalFileName: null, Width: 800, Height: 600, BlurDataUri: null)],
            collections: [],
            vocabularyTerms: [],
            products: []
        );

        var keys = PostImportPlanner.WorkImagesToHash([Folder("spring", PostJson("Spring", WorkOp("dawn", "Dawn", DawnSha256)))], [Dawn, other]);

        Assert.Equal([DawnKey], keys);
    }

    [Fact]
    public void AnAltTextTooLongForTheLibraryRefusesThePost()
    {
        var alt = new string('a', ArtistShopLimits.UploadedImageAltTextMaximumLength + 1);
        var image = $$$$"""{"insert":{"artshop-image":{"file":"image-1-original.jpg","alt":"{{{{alt}}}}"}}}""";

        var item = PlanOne(Folder("spring", PostJson("Spring", image), "image-1-original.jpg"));

        Assert.Equal(PostImportOutcome.Refused, item.Outcome);
        Assert.Single(item.Problems);
    }

    [Fact]
    public void FinishingSwapsEachPlaceholderForItsUpload()
    {
        const string image = """{"insert":{"artshop-image":{"file":"image-1-original.jpg","alt":"Pencil","caption":"Early"}}}""";
        var item = PlanOne(Folder("spring", PostJson("Spring", image), "image-1-original.jpg"));
        var placeholder = Assert.Single(item.ImagesByPlaceholder.Keys);
        var upload = new LibraryImageUploadResult("cccccccccccccccccccccccccccccccc", 1200, 900, "data:image/webp;base64,AAAA", "x");

        var body = PostImportPlanner.Finish(DocumentOf(item), new Dictionary<string, LibraryImageUploadResult> { [placeholder] = upload });

        var saved = Assert.Single(RichTextParser.Parse(body).Blocks.OfType<UploadedImageEmbedBlock>());
        Assert.Equal(("cccccccccccccccccccccccccccccccc", "Early"), (saved.StorageKey, saved.Caption));
    }

    private static RichTextDocument DocumentOf(PostImportItem item) => Unwrap.Value(item.Document);
}
