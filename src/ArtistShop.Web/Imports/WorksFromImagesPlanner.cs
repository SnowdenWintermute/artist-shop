using ArtistShop.Web.Database;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Imports;

public enum WorkFromImagesSkipReason : byte
{
    TooDeep = 1,
    SameNameInUpload = 2,
    TitleTooLong = 3,
    NoWebAddress = 4,
    WorkExists = 5,
}

public enum DroppedFolderCollectionType : byte
{
    Existing = 1,
    New = 2,
    // too long for a collection name, or no letters or numbers to make its web address from
    CannotBeCollection = 3,
}

// A folder of the drop that holds images itself, and the collection its name gives. Path runs from
// the dropped folder down, "My works/Seascapes". Existing is the collection for Existing, and
// SimilarCollectionName an existing collection a New one's name is close to, for the artist to check
public record DroppedFolder(
    string Path,
    int ImageCount,
    DroppedFolderCollectionType Type,
    Collection? Existing,
    string? SimilarCollectionName
)
{
    public string Name => Path.Split('/')[^1];

    public bool IsDropped => !Path.Contains('/');

    public CollectionName NewCollectionName => new(Name.Trim());
}

// One work to create: its title, the collection it joins, and its images in order. NewCollectionName is
// a collection its folder names that doesn't exist yet, made with the works
public record PlannedWorkFromImages(
    WorkName Name,
    IReadOnlyList<CollectionId> CollectionIds,
    CollectionName? NewCollectionName,
    IReadOnlyList<BulkImageCandidate> Images
);

public record SkippedImage(string Path, WorkFromImagesSkipReason Reason);

public record WorksFromImagesPlan(IReadOnlyList<PlannedWorkFromImages> Works, IReadOnlyList<SkippedImage> Skipped);

// what the artist chose in the collection dialog after a drop: the folders whose names are collections, and
// the collections for images dropped on their own
public record DropCollectionChoice(IReadOnlyList<DroppedFolder> CollectionFolders, IReadOnlyList<CollectionId> LooseCollectionIds);

// Turns dropped images into new works, titled by file name the way the Upload images page
// matches them, so that page can still attach whatever this one didn't get to send
public static class WorksFromImagesPlanner
{
    // the folders holding images, for the artist to say which are collections. A collection name is unique
    // by web address, so a folder matches the collection with its own, or would make a new one
    public static IReadOnlyList<DroppedFolder> Folders(IEnumerable<BulkImageCandidate> candidates, IReadOnlyList<Collection> collections) =>
        [
            .. candidates
                .Select(candidate => BulkImageDrop.FoldersOf(candidate.Path))
                .Where(folders => folders.Length is > 0 and <= BulkImageDrop.MaximumDirectoryDepth)
                .GroupBy(folders => string.Join('/', folders))
                .OrderBy(folder => folder.Key, StringComparer.InvariantCultureIgnoreCase)
                .Select(folder => ToDroppedFolder(folder.Key, folder.Count(), collections)),
        ];

    public static int LooseImageCount(IEnumerable<BulkImageCandidate> candidates) =>
        candidates.Count(candidate => BulkImageDrop.FoldersOf(candidate.Path).Length is 0);

    private static DroppedFolder ToDroppedFolder(string path, int imageCount, IReadOnlyList<Collection> collections)
    {
        var name = path.Split('/')[^1].Trim();
        var slug = CollectionSlug.FromName(name);

        if (collections.FirstOrDefault(existing => existing.Slug == slug) is { } existing)
        {
            return new DroppedFolder(path, imageCount, DroppedFolderCollectionType.Existing, existing, SimilarCollectionName: null);
        }

        if (name.Length > ArtistShopLimits.CollectionNameMaximumLength || slug.Value.Length is 0)
        {
            return new DroppedFolder(path, imageCount, DroppedFolderCollectionType.CannotBeCollection, Existing: null, SimilarCollectionName: null);
        }

        var similar = collections.FirstOrDefault(existing => CollectionNameSimilarity.AreNearDuplicates(name, existing.Name.Value));
        return new DroppedFolder(path, imageCount, DroppedFolderCollectionType.New, Existing: null, similar?.Name.Value);
    }

    // collectionFolders are the folders the artist chose, whose images join their collections, and
    // looseCollectionIds the collections chosen for images dropped on their own. Folders are read as the
    // Upload images page reads them, and names are compared across the whole drop, as that page
    // matches them
    public static WorksFromImagesPlan Plan(
        IEnumerable<BulkImageCandidate> candidates,
        IReadOnlyCollection<DroppedFolder> collectionFolders,
        IReadOnlyList<CollectionId> looseCollectionIds
    )
    {
        var skipped = new List<SkippedImage>();
        var placed = new List<(BulkImageCandidate Candidate, IReadOnlyList<CollectionId> CollectionIds, CollectionName? NewCollectionName)>();
        var collectionFoldersByPath = collectionFolders.ToDictionary(folder => folder.Path);

        foreach (var candidate in candidates.OrderBy(candidate => candidate.Path, StringComparer.InvariantCultureIgnoreCase))
        {
            var folders = BulkImageDrop.FoldersOf(candidate.Path);

            if (folders.Length > BulkImageDrop.MaximumDirectoryDepth)
            {
                skipped.Add(new SkippedImage(candidate.Path, WorkFromImagesSkipReason.TooDeep));
                continue;
            }

            if (folders.Length is 0)
            {
                placed.Add((candidate, looseCollectionIds, null));
                continue;
            }

            placed.Add(collectionFoldersByPath.GetValueOrDefault(string.Join('/', folders)) switch
            {
                { Existing: { } existing } => (candidate, [existing.Id], null),
                { Type: DroppedFolderCollectionType.New } folder => (candidate, [], folder.NewCollectionName),
                _ => (candidate, [], null),
            });
        }

        var named = placed.Select(file => (file.Candidate, file.CollectionIds, file.NewCollectionName, Name: WorkName.FromFileName(file.Candidate.Path))).ToList();
        var sameNames = named.GroupBy(file => file.Name.Value, DatabaseCollationComparer.Instance).ToList();

        foreach (var repeated in sameNames.Where(group => group.Count() > 1).SelectMany(group => group))
        {
            skipped.Add(new SkippedImage(repeated.Candidate.Path, WorkFromImagesSkipReason.SameNameInUpload));
        }

        var single = sameNames.Where(group => group.Count() is 1).Select(group => group.Single()).ToList();

        // "Dawn (2)" is another image of Dawn when Dawn is dropped too; otherwise it's a title.
        // Only a plain title takes extras, so "Dawn (2) (3)" can't join a row that is itself
        // Dawn's extra and vanish
        var plainTitles = single
            .Where(file => file.Name.AsExtraImage() is null)
            .Select(file => file.Name.Value)
            .ToHashSet(DatabaseCollationComparer.Instance);
        var extras = new List<(BulkImageCandidate Candidate, ExtraImageName Extra)>();

        foreach (var file in single)
        {
            if (file.Name.AsExtraImage() is { } extra && plainTitles.Contains(extra.Work.Value))
            {
                extras.Add((file.Candidate, extra));
            }
        }

        var extrasByTitle = extras.ToLookup(entry => entry.Extra.Work.Value, DatabaseCollationComparer.Instance);
        var extraIds = extras.Select(entry => entry.Candidate.Id).ToHashSet();

        var titles = single
            .Where(file => !extraIds.Contains(file.Candidate.Id))
            .OrderBy(file => file.Name.Value, StringComparer.InvariantCultureIgnoreCase);

        var works = new List<PlannedWorkFromImages>();

        foreach (var file in titles)
        {
            var images = extrasByTitle[file.Name.Value]
                .OrderBy(entry => entry.Extra.Number)
                .Select(entry => entry.Candidate)
                .Prepend(file.Candidate)
                .ToList();

            if (ProblemWith(file.Name) is { } problem)
            {
                skipped.AddRange(images.Select(image => new SkippedImage(image.Path, problem)));
                continue;
            }

            works.Add(new PlannedWorkFromImages(file.Name, file.CollectionIds, file.NewCollectionName, images));
        }

        return new WorksFromImagesPlan(works, skipped);
    }

    // once each as the database compares names, which is what the name check asks for
    public static IReadOnlyList<WorkName> NamesToCheck(WorksFromImagesPlan plan) =>
        [
            .. plan.Works
                .Select(work => work.Name.Value)
                .Distinct(DatabaseCollationComparer.Instance)
                .Select(name => new WorkName(name)),
        ];

    // a work of the type already has the title, most likely from dropping the same folder twice
    public static WorksFromImagesPlan WithoutExisting(
        WorksFromImagesPlan plan,
        IReadOnlyDictionary<WorkName, WorkNameMatch> matches
    )
    {
        var taken = matches
            .Where(entry => entry.Value.Type is not WorkNameMatchType.NoWork)
            .Select(entry => entry.Key.Value)
            .ToHashSet(DatabaseCollationComparer.Instance);

        return new WorksFromImagesPlan(
            [.. plan.Works.Where(work => !taken.Contains(work.Name.Value))],
            [
                .. plan.Skipped,
                .. plan.Works
                    .Where(work => taken.Contains(work.Name.Value))
                    .SelectMany(work => work.Images)
                    .Select(image => new SkippedImage(image.Path, WorkFromImagesSkipReason.WorkExists)),
            ]
        );
    }

    private static WorkFromImagesSkipReason? ProblemWith(WorkName name) =>
        !name.CanMatchAnWork ? WorkFromImagesSkipReason.TitleTooLong
        : WorkSlug.FromName(name.Value).Value.Length is 0 ? WorkFromImagesSkipReason.NoWebAddress
        : null;
}
