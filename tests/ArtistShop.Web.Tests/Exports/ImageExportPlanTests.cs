using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Exports;

namespace ArtistShop.Web.Tests.Exports;

public sealed class ImageExportPlanTests
{
    private static readonly ArtworkType Painting = new(new ArtworkTypeId(1), new ArtworkTypeName("Painting"));
    private static readonly ArtworkType Sculpture = new(new ArtworkTypeId(2), new ArtworkTypeName("Sculpture"));
    private static readonly Series Gardens = new(new SeriesId(21), new SeriesName("Gardens"), new SeriesSlug("gardens"));
    private static readonly Series Coast = new(new SeriesId(22), new SeriesName("Coast"), new SeriesSlug("coast"));

    private static Artwork MakeArtwork(
        string name,
        int imageCount = 1,
        ArtworkType? type = null,
        IReadOnlyList<Series>? series = null,
        string? slug = null
    ) =>
        new(
            new ArtworkId(Random.Shared.Next()),
            type ?? Painting,
            new ArtworkName(name),
            slug is null ? ArtworkSlug.FromName(name) : new ArtworkSlug(slug),
            description: null,
            dateCreated: null,
            dimensions: null,
            duration: null,
            Enumerable.Range(0, imageCount).Select(_ => new ArtworkImage(Guid.NewGuid().ToString("N"), null, 10, 10, null)),
            series ?? [],
            vocabularyTerms: [],
            products: []
        );

    private static List<string> Paths(ImageExportPart part) => [.. part.Entries.Select(entry => entry.PathWithoutExtension)];

    [Fact]
    public void OnePartPerTypeAndFirstSeriesWithNoSeriesLast()
    {
        var parts = ImageExportPlan.Parts(
            [
                MakeArtwork("Dawn", type: Sculpture),
                MakeArtwork("Rose", series: [Gardens, Coast]),
                MakeArtwork("Wave", series: [Coast]),
                MakeArtwork("Alone"),
                MakeArtwork("No images", imageCount: 0),
            ]
        );

        Assert.Equal(
            [("Painting", "Coast"), ("Painting", "Gardens"), ("Painting", null), ("Sculpture", null)],
            parts.Select(part => (part.Type.Name.Value, part.Series?.Name.Value))
        );
        Assert.Equal(["Painting/Gardens/Rose"], Paths(parts[1]));
        Assert.Equal(["Painting/Alone"], Paths(parts[2]));
    }

    [Fact]
    public void ExtraImagesAreNumberedInOrder()
    {
        var artwork = MakeArtwork("Dawn", imageCount: 3);

        var part = Assert.Single(ImageExportPlan.Parts([artwork]));

        Assert.Equal(["Painting/Dawn", "Painting/Dawn (2)", "Painting/Dawn (3)"], Paths(part));
        Assert.Equal(artwork.Images, part.Entries.Select(entry => entry.Image));
    }

    [Fact]
    public void TitlesSharedInAFolderIgnoringCaseUseSlugs()
    {
        var part = Assert.Single(
            ImageExportPlan.Parts([MakeArtwork("Dawn", slug: "dawn"), MakeArtwork("dawn", slug: "dawn-2"), MakeArtwork("Dusk")])
        );

        Assert.Equal(["Painting/dawn", "Painting/dawn-2", "Painting/Dusk"], Paths(part));
    }

    [Fact]
    public void ATitleMatchingAnotherArtworksExtraImageUsesSlugs()
    {
        var part = Assert.Single(
            ImageExportPlan.Parts([MakeArtwork("Dawn", imageCount: 2), MakeArtwork("Dawn (2)", slug: "dawn-2")])
        );

        Assert.Equal(["Painting/dawn", "Painting/dawn (2)", "Painting/dawn-2"], Paths(part));
    }

    [Fact]
    public void TheSameTitleInAnotherSeriesKeepsIt()
    {
        var parts = ImageExportPlan.Parts([MakeArtwork("Dawn", series: [Gardens]), MakeArtwork("Dawn", series: [Coast], slug: "dawn-2")]);

        Assert.Equal(["Painting/Coast/Dawn", "Painting/Gardens/Dawn"], parts.SelectMany(Paths));
    }

    [Fact]
    public void NamesThatCantBeFileNamesUseSlugs()
    {
        var parts = ImageExportPlan.Parts(
            [MakeArtwork("Yes/No", series: [new Series(new SeriesId(23), new SeriesName("A: B"), new SeriesSlug("a-b"))])]
        );

        Assert.Equal(["Painting/a-b/yes-no"], parts.SelectMany(Paths));
    }
}
