using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

public sealed class WorksFromImagesPlannerTests
{
    private static readonly Collection Seascapes = new(new CollectionId(3), new CollectionName("Seascapes, 1990s"), CollectionSlug.FromName("Seascapes, 1990s"));

    private static IReadOnlyList<BulkImageCandidate> Files(params string[] paths) =>
        [.. paths.Select(path => new BulkImageCandidate(path, path))];

    // "title: image, image", since lists inside tuples compare by reference
    private static List<string> Works(WorksFromImagesPlan plan) =>
        [.. plan.Works.Select(work => $"{work.Name.Value}: {string.Join(", ", work.Images.Select(image => image.Path))}")];

    private static WorkFromImagesSkipReason ReasonFor(WorksFromImagesPlan plan, string path) =>
        Assert.Single(plan.Skipped, skipped => skipped.Path == path).Reason;

    [Fact]
    public void TitlesAreFileNamesWithoutTheirExtension()
    {
        var plan = WorksFromImagesPlanner.Plan(Files("/Painting/sunset_over-bay.jpg", "loose.png"), collectionFolders: [], looseCollectionIds: []);

        Assert.Equal(["loose", "sunset_over-bay"], plan.Works.Select(work => work.Name.Value).Order());
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void NumberedFilesAreMoreImagesOfTheTitleInTheSameFolder()
    {
        var plan = WorksFromImagesPlanner.Plan(
            Files("/Painting/Dawn (10).jpg", "/Painting/Dawn.jpg", "/Painting/Dawn (2).jpg", "/Painting/Dusk (2).jpg"),
            collectionFolders: [], looseCollectionIds: []
        );

        Assert.Equal(
            ["Dawn: /Painting/Dawn.jpg, /Painting/Dawn (2).jpg, /Painting/Dawn (10).jpg", "Dusk (2): /Painting/Dusk (2).jpg"],
            Works(plan)
        );
    }

    [Fact]
    public void ANumberedTitleThatIsItselfAnExtraDoesNotTakeExtras()
    {
        var plan = WorksFromImagesPlanner.Plan(Files("/P/Dawn.jpg", "/P/Dawn (2).jpg", "/P/Dawn (2) (3).jpg"), collectionFolders: [], looseCollectionIds: []);

        Assert.Equal(
            ["Dawn: /P/Dawn.jpg, /P/Dawn (2).jpg", "Dawn (2) (3): /P/Dawn (2) (3).jpg"],
            Works(plan)
        );
    }

    // as the Upload images page reads a drop, so that page can still finish what this one didn't send
    [Fact]
    public void TheSameNameAnywhereInTheDropIsSkipped()
    {
        var plan = WorksFromImagesPlanner.Plan(
            Files("/P/a/Untitled.jpg", "/P/b/Untitled.jpg", "/P/c/Dawn.jpg", "/P/c/dawn.png", "/P/Dusk.jpg"),
            collectionFolders: [], looseCollectionIds: []
        );

        Assert.Equal(["Dusk"], plan.Works.Select(work => work.Name.Value));
        Assert.All(
            ["/P/a/Untitled.jpg", "/P/b/Untitled.jpg", "/P/c/Dawn.jpg", "/P/c/dawn.png"],
            path => Assert.Equal(WorkFromImagesSkipReason.SameNameInUpload, ReasonFor(plan, path))
        );
    }

    [Fact]
    public void NumberedFilesJoinTheirTitleFromAnotherFolder()
    {
        var plan = WorksFromImagesPlanner.Plan(Files("/P/Dawn.jpg", "/P/more/Dawn (2).jpg"), collectionFolders: [], looseCollectionIds: []);

        Assert.Equal(["Dawn: /P/Dawn.jpg, /P/more/Dawn (2).jpg"], Works(plan));
    }

    [Fact]
    public void WithoutCollectionFoldersTheDroppedFolderAndOneLevelAreTaken()
    {
        var plan = WorksFromImagesPlanner.Plan(
            Files("/P/Loose.jpg", "/P/Seascapes 1990s/Wave.jpg", "/P/Seascapes 1990s/thumbs/Wave small.jpg"),
            collectionFolders: [], looseCollectionIds: []
        );

        Assert.Equal(["Loose", "Wave"], plan.Works.Select(work => work.Name.Value));
        Assert.All(plan.Works, work => Assert.Empty(work.CollectionIds));
        Assert.Equal(WorkFromImagesSkipReason.TooDeep, ReasonFor(plan, "/P/Seascapes 1990s/thumbs/Wave small.jpg"));
    }

    [Fact]
    public void FoldersHoldingImagesAreListedWithTheCollectionsTheirNamesGive()
    {
        var folders = WorksFromImagesPlanner.Folders(
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
                ("Paintings", 1, DroppedFolderCollectionType.New, true),
                ("Paintings/!!!", 1, DroppedFolderCollectionType.CannotBeCollection, false),
                ("Paintings/seascapes 1990s", 2, DroppedFolderCollectionType.Existing, false),
                ("Paintings/Seascapes, 1999", 1, DroppedFolderCollectionType.New, false),
            ],
            folders.Select(folder => (folder.Path, folder.ImageCount, folder.Type, folder.IsDropped))
        );
        Assert.Equal(Seascapes, folders[2].Existing);
        // one character from an existing collection, which the artist may have meant
        Assert.Equal(Seascapes.Name.Value, folders[3].SimilarCollectionName);
    }

    // "new: Name" for a collection made with the works
    private static List<(string Title, string Collection)> CollectionsOf(WorksFromImagesPlan plan) =>
        [
            .. plan.Works.Select(work => (
                work.Name.Value,
                string.Join(", ", work.CollectionIds.Select(id => $"{id.Value}").Concat(work.NewCollectionName is { } name ? [$"new: {name.Value}"] : []))
            )),
        ];

    [Fact]
    public void ImagesInChosenFoldersJoinTheirCollectionsAndOthersJoinNone()
    {
        var files = Files("/Paintings/Loose.jpg", "/Paintings/seascapes 1990s/Wave.jpg", "/Paintings/Portraits/Face.jpg");
        var folders = WorksFromImagesPlanner.Folders(files, [Seascapes]);

        var plan = WorksFromImagesPlanner.Plan(files, [.. folders.Where(folder => !folder.IsDropped)], looseCollectionIds: []);

        Assert.Equal([("Face", "new: Portraits"), ("Loose", ""), ("Wave", "3")], CollectionsOf(plan));
        Assert.Empty(plan.Skipped);
    }

    // images dropped on their own, rather than in a folder, join every collection chosen for them
    [Fact]
    public void LooseImagesJoinTheCollectionChosenForThem()
    {
        var plan = WorksFromImagesPlanner.Plan(
            Files("Dawn.jpg", "/Paintings/Dusk.jpg"),
            collectionFolders: [],
            looseCollectionIds: [new CollectionId(3), new CollectionId(5)]
        );

        Assert.Equal([("Dawn", "3, 5"), ("Dusk", "")], CollectionsOf(plan));
        Assert.Equal(1, WorksFromImagesPlanner.LooseImageCount(Files("Dawn.jpg", "/Paintings/Dusk.jpg")));
    }

    [Fact]
    public void ATitleThatCantBeAWebAddressIsSkippedWithItsExtras()
    {
        var plan = WorksFromImagesPlanner.Plan(Files("/P/!!!.jpg", "/P/!!! (2).jpg"), collectionFolders: [], looseCollectionIds: []);

        Assert.Empty(plan.Works);
        Assert.Equal(WorkFromImagesSkipReason.NoWebAddress, ReasonFor(plan, "/P/!!!.jpg"));
        Assert.Equal(WorkFromImagesSkipReason.NoWebAddress, ReasonFor(plan, "/P/!!! (2).jpg"));
    }

    [Fact]
    public void ATitleAnWorkAlreadyHasIsSkipped()
    {
        var plan = WorksFromImagesPlanner.Plan(Files("/P/Dawn.jpg", "/P/Dawn (2).jpg", "/P/Dusk.jpg"), collectionFolders: [], looseCollectionIds: []);

        Assert.Equal(["Dawn", "Dusk"], WorksFromImagesPlanner.NamesToCheck(plan).Select(name => name.Value));

        var remaining = WorksFromImagesPlanner.WithoutExisting(
            plan,
            new Dictionary<WorkName, WorkNameMatch>
            {
                [new WorkName("Dawn")] = new(WorkNameMatchType.WorkWithImages, [new WorkId(1)]),
                [new WorkName("Dusk")] = new(WorkNameMatchType.NoWork, []),
            }
        );

        Assert.Equal(["Dusk"], remaining.Works.Select(work => work.Name.Value));
        Assert.Equal(WorkFromImagesSkipReason.WorkExists, ReasonFor(remaining, "/P/Dawn.jpg"));
        Assert.Equal(WorkFromImagesSkipReason.WorkExists, ReasonFor(remaining, "/P/Dawn (2).jpg"));
    }
}
