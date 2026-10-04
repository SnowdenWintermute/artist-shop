using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

public sealed class ArtworksFromImagesPlannerTests
{
    private static readonly Series Seascapes = new(new SeriesId(3), new SeriesName("Seascapes, 1990s"), SeriesSlug.FromName("Seascapes, 1990s"));

    private static IReadOnlyList<BulkImageCandidate> Files(params string[] paths) =>
        [.. paths.Select(path => new BulkImageCandidate(path, path))];

    // "title: image, image", since lists inside tuples compare by reference
    private static List<string> Artworks(ArtworksFromImagesPlan plan) =>
        [.. plan.Artworks.Select(artwork => $"{artwork.Name.Value}: {string.Join(", ", artwork.Images.Select(image => image.Path))}")];

    private static ArtworkFromImagesSkipReason ReasonFor(ArtworksFromImagesPlan plan, string path) =>
        Assert.Single(plan.Skipped, skipped => skipped.Path == path).Reason;

    [Fact]
    public void TitlesAreFileNamesWithoutTheirExtension()
    {
        var plan = ArtworksFromImagesPlanner.Plan(Files("/Painting/sunset_over-bay.jpg", "loose.png"), seriesForFolders: null);

        Assert.Equal(["loose", "sunset_over-bay"], plan.Artworks.Select(artwork => artwork.Name.Value).Order());
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void NumberedFilesAreMoreImagesOfTheTitleInTheSameFolder()
    {
        var plan = ArtworksFromImagesPlanner.Plan(
            Files("/Painting/Dawn (10).jpg", "/Painting/Dawn.jpg", "/Painting/Dawn (2).jpg", "/Painting/Dusk (2).jpg"),
            seriesForFolders: null
        );

        Assert.Equal(
            ["Dawn: /Painting/Dawn.jpg, /Painting/Dawn (2).jpg, /Painting/Dawn (10).jpg", "Dusk (2): /Painting/Dusk (2).jpg"],
            Artworks(plan)
        );
    }

    [Fact]
    public void ANumberedTitleThatIsItselfAnExtraDoesNotTakeExtras()
    {
        var plan = ArtworksFromImagesPlanner.Plan(Files("/P/Dawn.jpg", "/P/Dawn (2).jpg", "/P/Dawn (2) (3).jpg"), seriesForFolders: null);

        Assert.Equal(
            ["Dawn: /P/Dawn.jpg, /P/Dawn (2).jpg", "Dawn (2) (3): /P/Dawn (2) (3).jpg"],
            Artworks(plan)
        );
    }

    [Fact]
    public void TheSameNameInOneFolderIsSkippedButInTwoFoldersIsTwoArtworks()
    {
        var plan = ArtworksFromImagesPlanner.Plan(
            Files("/P/a/Untitled.jpg", "/P/b/Untitled.jpg", "/P/c/Dawn.jpg", "/P/c/dawn.png"),
            seriesForFolders: null
        );

        Assert.Equal(["Untitled", "Untitled"], plan.Artworks.Select(artwork => artwork.Name.Value));
        Assert.Equal(ArtworkFromImagesSkipReason.SameNameInFolder, ReasonFor(plan, "/P/c/Dawn.jpg"));
        Assert.Equal(ArtworkFromImagesSkipReason.SameNameInFolder, ReasonFor(plan, "/P/c/dawn.png"));
    }

    [Fact]
    public void WithoutSeriesFoldersAnyDepthIsTakenAndNoSeriesIsSet()
    {
        var plan = ArtworksFromImagesPlanner.Plan(Files("/P/Seascapes 1990s/x/y/Deep.jpg"), seriesForFolders: null);

        var artwork = Assert.Single(plan.Artworks);
        Assert.Null(artwork.SeriesId);
    }

    [Fact]
    public void ASubfolderNamesItsSeriesByWebAddress()
    {
        var plan = ArtworksFromImagesPlanner.Plan(
            Files("/Paintings/seascapes 1990s/Wave.jpg", "/Paintings/Loose.jpg", "/Paintings/Portraits/Face.jpg", "/Paintings/a/b/Deep.jpg"),
            seriesForFolders: [Seascapes]
        );

        Assert.Equal(
            [("Loose", (SeriesId?)null), ("Wave", Seascapes.Id)],
            plan.Artworks.Select(artwork => (artwork.Name.Value, artwork.SeriesId)).OrderBy(entry => entry.Value)
        );
        Assert.Equal(ArtworkFromImagesSkipReason.UnknownSeriesFolder, ReasonFor(plan, "/Paintings/Portraits/Face.jpg"));
        Assert.Equal(ArtworkFromImagesSkipReason.TooDeep, ReasonFor(plan, "/Paintings/a/b/Deep.jpg"));
    }

    // dropping one series' own folder puts its images in that series
    [Fact]
    public void TheDroppedFolderNamesASeriesWhenOneMatches()
    {
        var plan = ArtworksFromImagesPlanner.Plan(Files("/Seascapes 1990s/Wave.jpg", "Loose.jpg"), seriesForFolders: [Seascapes]);

        Assert.Equal(
            [("Loose", (SeriesId?)null), ("Wave", Seascapes.Id)],
            plan.Artworks.Select(artwork => (artwork.Name.Value, artwork.SeriesId)).OrderBy(entry => entry.Value)
        );
    }

    [Fact]
    public void ATitleThatCantBeAWebAddressIsSkippedWithItsExtras()
    {
        var plan = ArtworksFromImagesPlanner.Plan(Files("/P/!!!.jpg", "/P/!!! (2).jpg"), seriesForFolders: null);

        Assert.Empty(plan.Artworks);
        Assert.Equal(ArtworkFromImagesSkipReason.NoWebAddress, ReasonFor(plan, "/P/!!!.jpg"));
        Assert.Equal(ArtworkFromImagesSkipReason.NoWebAddress, ReasonFor(plan, "/P/!!! (2).jpg"));
    }

    [Fact]
    public void ATitleAnArtworkAlreadyHasIsSkipped()
    {
        var plan = ArtworksFromImagesPlanner.Plan(Files("/P/Dawn.jpg", "/P/Dawn (2).jpg", "/P/Dusk.jpg"), seriesForFolders: null);

        Assert.Equal(["Dawn", "Dusk"], ArtworksFromImagesPlanner.NamesToCheck(plan).Select(name => name.Value));

        var remaining = ArtworksFromImagesPlanner.WithoutExisting(
            plan,
            new Dictionary<ArtworkName, ArtworkNameMatch>
            {
                [new ArtworkName("Dawn")] = new(ArtworkNameMatchType.ArtworkWithImages, [new ArtworkId(1)]),
                [new ArtworkName("Dusk")] = new(ArtworkNameMatchType.NoArtwork, []),
            }
        );

        Assert.Equal(["Dusk"], remaining.Artworks.Select(artwork => artwork.Name.Value));
        Assert.Equal(ArtworkFromImagesSkipReason.ArtworkExists, ReasonFor(remaining, "/P/Dawn.jpg"));
        Assert.Equal(ArtworkFromImagesSkipReason.ArtworkExists, ReasonFor(remaining, "/P/Dawn (2).jpg"));
    }
}
