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
        var plan = ArtworksFromImagesPlanner.Plan(Files("/Painting/sunset_over-bay.jpg", "loose.png"), seriesFolders: [], looseSeriesIds: []);

        Assert.Equal(["loose", "sunset_over-bay"], plan.Artworks.Select(artwork => artwork.Name.Value).Order());
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void NumberedFilesAreMoreImagesOfTheTitleInTheSameFolder()
    {
        var plan = ArtworksFromImagesPlanner.Plan(
            Files("/Painting/Dawn (10).jpg", "/Painting/Dawn.jpg", "/Painting/Dawn (2).jpg", "/Painting/Dusk (2).jpg"),
            seriesFolders: [], looseSeriesIds: []
        );

        Assert.Equal(
            ["Dawn: /Painting/Dawn.jpg, /Painting/Dawn (2).jpg, /Painting/Dawn (10).jpg", "Dusk (2): /Painting/Dusk (2).jpg"],
            Artworks(plan)
        );
    }

    [Fact]
    public void ANumberedTitleThatIsItselfAnExtraDoesNotTakeExtras()
    {
        var plan = ArtworksFromImagesPlanner.Plan(Files("/P/Dawn.jpg", "/P/Dawn (2).jpg", "/P/Dawn (2) (3).jpg"), seriesFolders: [], looseSeriesIds: []);

        Assert.Equal(
            ["Dawn: /P/Dawn.jpg, /P/Dawn (2).jpg", "Dawn (2) (3): /P/Dawn (2) (3).jpg"],
            Artworks(plan)
        );
    }

    // as the Upload images page reads a drop, so that page can still finish what this one didn't send
    [Fact]
    public void TheSameNameAnywhereInTheDropIsSkipped()
    {
        var plan = ArtworksFromImagesPlanner.Plan(
            Files("/P/a/Untitled.jpg", "/P/b/Untitled.jpg", "/P/c/Dawn.jpg", "/P/c/dawn.png", "/P/Dusk.jpg"),
            seriesFolders: [], looseSeriesIds: []
        );

        Assert.Equal(["Dusk"], plan.Artworks.Select(artwork => artwork.Name.Value));
        Assert.All(
            ["/P/a/Untitled.jpg", "/P/b/Untitled.jpg", "/P/c/Dawn.jpg", "/P/c/dawn.png"],
            path => Assert.Equal(ArtworkFromImagesSkipReason.SameNameInUpload, ReasonFor(plan, path))
        );
    }

    [Fact]
    public void NumberedFilesJoinTheirTitleFromAnotherFolder()
    {
        var plan = ArtworksFromImagesPlanner.Plan(Files("/P/Dawn.jpg", "/P/more/Dawn (2).jpg"), seriesFolders: [], looseSeriesIds: []);

        Assert.Equal(["Dawn: /P/Dawn.jpg, /P/more/Dawn (2).jpg"], Artworks(plan));
    }

    [Fact]
    public void WithoutSeriesFoldersTheDroppedFolderAndOneLevelAreTaken()
    {
        var plan = ArtworksFromImagesPlanner.Plan(
            Files("/P/Loose.jpg", "/P/Seascapes 1990s/Wave.jpg", "/P/Seascapes 1990s/thumbs/Wave small.jpg"),
            seriesFolders: [], looseSeriesIds: []
        );

        Assert.Equal(["Loose", "Wave"], plan.Artworks.Select(artwork => artwork.Name.Value));
        Assert.All(plan.Artworks, artwork => Assert.Empty(artwork.SeriesIds));
        Assert.Equal(ArtworkFromImagesSkipReason.TooDeep, ReasonFor(plan, "/P/Seascapes 1990s/thumbs/Wave small.jpg"));
    }

    [Fact]
    public void FoldersHoldingImagesAreListedWithTheSeriesTheirNamesGive()
    {
        var folders = ArtworksFromImagesPlanner.Folders(
            Files(
                "/Paintings/Loose.jpg",
                "/Paintings/seascapes 1990s/Wave.jpg",
                "/Paintings/seascapes 1990s/Tide.jpg",
                "/Paintings/Seascapes, 1999/Gull.jpg",
                "/Paintings/!!!/Odd.jpg",
                "/Paintings/a/b/Deep.jpg",
                "Alone.jpg"
            ),
            [Seascapes]
        );

        Assert.Equal(
            [
                ("Paintings", 1, DroppedFolderSeriesType.New, true),
                ("Paintings/!!!", 1, DroppedFolderSeriesType.CannotBeSeries, false),
                ("Paintings/seascapes 1990s", 2, DroppedFolderSeriesType.Existing, false),
                ("Paintings/Seascapes, 1999", 1, DroppedFolderSeriesType.New, false),
            ],
            folders.Select(folder => (folder.Path, folder.ImageCount, folder.Type, folder.IsDropped))
        );
        Assert.Equal(Seascapes, folders[2].Existing);
        // one character from an existing series, which the artist may have meant
        Assert.Equal(Seascapes.Name.Value, folders[3].SimilarSeriesName);
    }

    // "new: Name" for a series made with the artworks
    private static List<(string Title, string Series)> SeriesOf(ArtworksFromImagesPlan plan) =>
        [
            .. plan.Artworks.Select(artwork => (
                artwork.Name.Value,
                string.Join(", ", artwork.SeriesIds.Select(id => $"{id.Value}").Concat(artwork.NewSeriesName is { } name ? [$"new: {name.Value}"] : []))
            )),
        ];

    [Fact]
    public void ImagesInChosenFoldersJoinTheirSeriesAndOthersJoinNone()
    {
        var files = Files("/Paintings/Loose.jpg", "/Paintings/seascapes 1990s/Wave.jpg", "/Paintings/Portraits/Face.jpg");
        var folders = ArtworksFromImagesPlanner.Folders(files, [Seascapes]);

        var plan = ArtworksFromImagesPlanner.Plan(files, [.. folders.Where(folder => !folder.IsDropped)], looseSeriesIds: []);

        Assert.Equal([("Face", "new: Portraits"), ("Loose", ""), ("Wave", "3")], SeriesOf(plan));
        Assert.Empty(plan.Skipped);
    }

    // images dropped on their own, rather than in a folder, join every series chosen for them
    [Fact]
    public void LooseImagesJoinTheSeriesChosenForThem()
    {
        var plan = ArtworksFromImagesPlanner.Plan(
            Files("Dawn.jpg", "/Paintings/Dusk.jpg"),
            seriesFolders: [],
            looseSeriesIds: [new SeriesId(3), new SeriesId(5)]
        );

        Assert.Equal([("Dawn", "3, 5"), ("Dusk", "")], SeriesOf(plan));
        Assert.Equal(1, ArtworksFromImagesPlanner.LooseImageCount(Files("Dawn.jpg", "/Paintings/Dusk.jpg")));
    }

    [Fact]
    public void ATitleThatCantBeAWebAddressIsSkippedWithItsExtras()
    {
        var plan = ArtworksFromImagesPlanner.Plan(Files("/P/!!!.jpg", "/P/!!! (2).jpg"), seriesFolders: [], looseSeriesIds: []);

        Assert.Empty(plan.Artworks);
        Assert.Equal(ArtworkFromImagesSkipReason.NoWebAddress, ReasonFor(plan, "/P/!!!.jpg"));
        Assert.Equal(ArtworkFromImagesSkipReason.NoWebAddress, ReasonFor(plan, "/P/!!! (2).jpg"));
    }

    [Fact]
    public void ATitleAnArtworkAlreadyHasIsSkipped()
    {
        var plan = ArtworksFromImagesPlanner.Plan(Files("/P/Dawn.jpg", "/P/Dawn (2).jpg", "/P/Dusk.jpg"), seriesFolders: [], looseSeriesIds: []);

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
