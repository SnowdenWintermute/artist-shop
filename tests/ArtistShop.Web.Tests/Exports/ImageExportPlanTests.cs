using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Exports;

namespace ArtistShop.Web.Tests.Exports;

public sealed class ImageExportPlanTests
{
    private static readonly WorkType Painting = new(new WorkTypeId(1), new WorkTypeName("Painting"));
    private static readonly WorkType Sculpture = new(new WorkTypeId(2), new WorkTypeName("Sculpture"));
    private static readonly Collection Gardens = new(new CollectionId(21), new CollectionName("Gardens"), new CollectionSlug("gardens"));
    private static readonly Collection Coast = new(new CollectionId(22), new CollectionName("Coast"), new CollectionSlug("coast"));

    private static Work MakeWork(
        string name,
        int imageCount = 1,
        WorkType? type = null,
        IReadOnlyList<Collection>? collections = null,
        string? slug = null
    ) =>
        new(
            new WorkId(Random.Shared.Next()),
            type ?? Painting,
            new WorkName(name),
            slug is null ? WorkSlug.FromName(name) : new WorkSlug(slug),
            description: null,
            dateCreated: null,
            dimensions: null,
            duration: null,
            Enumerable.Range(0, imageCount).Select(_ => new WorkImage(Guid.NewGuid().ToString("N"), null, 10, 10, null)),
            collections ?? [],
            vocabularyTerms: [],
            products: []
        );

    private static List<string> Paths(ImageExportPart part) => [.. part.Entries.Select(entry => entry.PathWithoutExtension)];

    [Fact]
    public void OnePartPerTypeAndFirstCollectionWithNoCollectionLast()
    {
        var parts = ImageExportPlan.Parts(
            [
                MakeWork("Dawn", type: Sculpture),
                MakeWork("Rose", collections: [Gardens, Coast]),
                MakeWork("Wave", collections: [Coast]),
                MakeWork("Alone"),
                MakeWork("No images", imageCount: 0),
            ]
        );

        Assert.Equal(
            [("Painting", "Coast"), ("Painting", "Gardens"), ("Painting", null), ("Sculpture", null)],
            parts.Select(part => (part.Type.Name.Value, part.Collection?.Name.Value))
        );
        Assert.Equal(["Painting/Gardens/Rose"], Paths(parts[1]));
        Assert.Equal(["Painting/Alone"], Paths(parts[2]));
    }

    [Fact]
    public void ExtraImagesAreNumberedInOrder()
    {
        var work = MakeWork("Dawn", imageCount: 3);

        var part = Assert.Single(ImageExportPlan.Parts([work]));

        Assert.Equal(["Painting/Dawn", "Painting/Dawn (2)", "Painting/Dawn (3)"], Paths(part));
        Assert.Equal(work.Images, part.Entries.Select(entry => entry.Image));
    }

    [Fact]
    public void TitlesSharedInAFolderIgnoringCaseUseSlugs()
    {
        var part = Assert.Single(
            ImageExportPlan.Parts([MakeWork("Dawn", slug: "dawn"), MakeWork("dawn", slug: "dawn-2"), MakeWork("Dusk")])
        );

        Assert.Equal(["Painting/dawn", "Painting/dawn-2", "Painting/Dusk"], Paths(part));
    }

    [Fact]
    public void ATitleMatchingAnotherWorksExtraImageUsesSlugs()
    {
        var part = Assert.Single(
            ImageExportPlan.Parts([MakeWork("Dawn", imageCount: 2), MakeWork("Dawn (2)", slug: "dawn-2")])
        );

        Assert.Equal(["Painting/dawn", "Painting/dawn (2)", "Painting/dawn-2"], Paths(part));
    }

    [Fact]
    public void TheSameTitleInAnotherCollectionKeepsIt()
    {
        var parts = ImageExportPlan.Parts([MakeWork("Dawn", collections: [Gardens]), MakeWork("Dawn", collections: [Coast], slug: "dawn-2")]);

        Assert.Equal(["Painting/Coast/Dawn", "Painting/Gardens/Dawn"], parts.SelectMany(Paths));
    }

    [Fact]
    public void NamesThatCantBeFileNamesUseSlugs()
    {
        var parts = ImageExportPlan.Parts(
            [MakeWork("Yes/No", collections: [new Collection(new CollectionId(23), new CollectionName("A: B"), new CollectionSlug("a-b"))])]
        );

        Assert.Equal(["Painting/a-b/yes-no"], parts.SelectMany(Paths));
    }
}
