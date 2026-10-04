using ArtistShop.Web.Database;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Imports;

public enum ArtworkFromImagesSkipReason : byte
{
    TooDeep = 1,
    SameNameInUpload = 2,
    TitleTooLong = 3,
    NoWebAddress = 4,
    ArtworkExists = 5,
}

public enum DroppedFolderSeriesType : byte
{
    Existing = 1,
    New = 2,
    // too long for a series name, or no letters or numbers to make its web address from
    CannotBeSeries = 3,
}

// A folder of the drop that holds images itself, and the series its name gives. Path runs from
// the dropped folder down, "My artworks/Seascapes". Existing is the series for Existing, and
// SimilarSeriesName an existing series a New one's name is close to, for the artist to check
public record DroppedFolder(
    string Path,
    int ImageCount,
    DroppedFolderSeriesType Type,
    Series? Existing,
    string? SimilarSeriesName
)
{
    public string Name => Path.Split('/')[^1];

    public bool IsDropped => !Path.Contains('/');

    public SeriesName NewSeriesName => new(Name.Trim());
}

// One artwork to create: its title, the series it joins, and its images in order. NewSeriesName is
// a series its folder names that doesn't exist yet, made with the artworks
public record PlannedArtworkFromImages(
    ArtworkName Name,
    IReadOnlyList<SeriesId> SeriesIds,
    SeriesName? NewSeriesName,
    IReadOnlyList<BulkImageCandidate> Images
);

public record SkippedImage(string Path, ArtworkFromImagesSkipReason Reason);

public record ArtworksFromImagesPlan(IReadOnlyList<PlannedArtworkFromImages> Artworks, IReadOnlyList<SkippedImage> Skipped);

// what the artist chose in the series dialog after a drop: the folders whose names are series, and
// the series for images dropped on their own
public record DropSeriesChoice(IReadOnlyList<DroppedFolder> SeriesFolders, IReadOnlyList<SeriesId> LooseSeriesIds);

// Turns dropped images into new artworks, titled by file name the way the Upload images page
// matches them, so that page can still attach whatever this one didn't get to send
public static class ArtworksFromImagesPlanner
{
    // the folders holding images, for the artist to say which are series. A series name is unique
    // by web address, so a folder matches the series with its own, or would make a new one
    public static IReadOnlyList<DroppedFolder> Folders(IEnumerable<BulkImageCandidate> candidates, IReadOnlyList<Series> series) =>
        [
            .. candidates
                .Select(candidate => BulkImageDrop.FoldersOf(candidate.Path))
                .Where(folders => folders.Length is > 0 and <= BulkImageDrop.MaximumDirectoryDepth)
                .GroupBy(folders => string.Join('/', folders))
                .OrderBy(folder => folder.Key, StringComparer.InvariantCultureIgnoreCase)
                .Select(folder => ToDroppedFolder(folder.Key, folder.Count(), series)),
        ];

    public static int LooseImageCount(IEnumerable<BulkImageCandidate> candidates) =>
        candidates.Count(candidate => BulkImageDrop.FoldersOf(candidate.Path).Length is 0);

    private static DroppedFolder ToDroppedFolder(string path, int imageCount, IReadOnlyList<Series> series)
    {
        var name = path.Split('/')[^1].Trim();
        var slug = SeriesSlug.FromName(name);

        if (series.FirstOrDefault(existing => existing.Slug == slug) is { } existing)
        {
            return new DroppedFolder(path, imageCount, DroppedFolderSeriesType.Existing, existing, SimilarSeriesName: null);
        }

        if (name.Length > ArtistShopLimits.SeriesNameMaximumLength || slug.Value.Length is 0)
        {
            return new DroppedFolder(path, imageCount, DroppedFolderSeriesType.CannotBeSeries, Existing: null, SimilarSeriesName: null);
        }

        var similar = series.FirstOrDefault(existing => SeriesNameSimilarity.AreNearDuplicates(name, existing.Name.Value));
        return new DroppedFolder(path, imageCount, DroppedFolderSeriesType.New, Existing: null, similar?.Name.Value);
    }

    // seriesFolders are the folders the artist chose, whose images join their series, and
    // looseSeriesIds the series chosen for images dropped on their own. Folders are read as the
    // Upload images page reads them, and names are compared across the whole drop, as that page
    // matches them
    public static ArtworksFromImagesPlan Plan(
        IEnumerable<BulkImageCandidate> candidates,
        IReadOnlyCollection<DroppedFolder> seriesFolders,
        IReadOnlyList<SeriesId> looseSeriesIds
    )
    {
        var skipped = new List<SkippedImage>();
        var placed = new List<(BulkImageCandidate Candidate, IReadOnlyList<SeriesId> SeriesIds, SeriesName? NewSeriesName)>();
        var seriesFoldersByPath = seriesFolders.ToDictionary(folder => folder.Path);

        foreach (var candidate in candidates.OrderBy(candidate => candidate.Path, StringComparer.InvariantCultureIgnoreCase))
        {
            var folders = BulkImageDrop.FoldersOf(candidate.Path);

            if (folders.Length > BulkImageDrop.MaximumDirectoryDepth)
            {
                skipped.Add(new SkippedImage(candidate.Path, ArtworkFromImagesSkipReason.TooDeep));
                continue;
            }

            if (folders.Length is 0)
            {
                placed.Add((candidate, looseSeriesIds, null));
                continue;
            }

            placed.Add(seriesFoldersByPath.GetValueOrDefault(string.Join('/', folders)) switch
            {
                { Existing: { } existing } => (candidate, [existing.Id], null),
                { Type: DroppedFolderSeriesType.New } folder => (candidate, [], folder.NewSeriesName),
                _ => (candidate, [], null),
            });
        }

        var named = placed.Select(file => (file.Candidate, file.SeriesIds, file.NewSeriesName, Name: ArtworkName.FromFileName(file.Candidate.Path))).ToList();
        var sameNames = named.GroupBy(file => file.Name.Value, DatabaseCollationComparer.Instance).ToList();

        foreach (var repeated in sameNames.Where(group => group.Count() > 1).SelectMany(group => group))
        {
            skipped.Add(new SkippedImage(repeated.Candidate.Path, ArtworkFromImagesSkipReason.SameNameInUpload));
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
            if (file.Name.AsExtraImage() is { } extra && plainTitles.Contains(extra.Artwork.Value))
            {
                extras.Add((file.Candidate, extra));
            }
        }

        var extrasByTitle = extras.ToLookup(entry => entry.Extra.Artwork.Value, DatabaseCollationComparer.Instance);
        var extraIds = extras.Select(entry => entry.Candidate.Id).ToHashSet();

        var titles = single
            .Where(file => !extraIds.Contains(file.Candidate.Id))
            .OrderBy(file => file.Name.Value, StringComparer.InvariantCultureIgnoreCase);

        var artworks = new List<PlannedArtworkFromImages>();

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

            artworks.Add(new PlannedArtworkFromImages(file.Name, file.SeriesIds, file.NewSeriesName, images));
        }

        return new ArtworksFromImagesPlan(artworks, skipped);
    }

    // once each as the database compares names, which is what the name check asks for
    public static IReadOnlyList<ArtworkName> NamesToCheck(ArtworksFromImagesPlan plan) =>
        [
            .. plan.Artworks
                .Select(artwork => artwork.Name.Value)
                .Distinct(DatabaseCollationComparer.Instance)
                .Select(name => new ArtworkName(name)),
        ];

    // an artwork of the type already has the title, most likely from dropping the same folder twice
    public static ArtworksFromImagesPlan WithoutExisting(
        ArtworksFromImagesPlan plan,
        IReadOnlyDictionary<ArtworkName, ArtworkNameMatch> matches
    )
    {
        var taken = matches
            .Where(entry => entry.Value.Type is not ArtworkNameMatchType.NoArtwork)
            .Select(entry => entry.Key.Value)
            .ToHashSet(DatabaseCollationComparer.Instance);

        return new ArtworksFromImagesPlan(
            [.. plan.Artworks.Where(artwork => !taken.Contains(artwork.Name.Value))],
            [
                .. plan.Skipped,
                .. plan.Artworks
                    .Where(artwork => taken.Contains(artwork.Name.Value))
                    .SelectMany(artwork => artwork.Images)
                    .Select(image => new SkippedImage(image.Path, ArtworkFromImagesSkipReason.ArtworkExists)),
            ]
        );
    }

    private static ArtworkFromImagesSkipReason? ProblemWith(ArtworkName name) =>
        !name.CanMatchAnArtwork ? ArtworkFromImagesSkipReason.TitleTooLong
        : ArtworkSlug.FromName(name.Value).Value.Length is 0 ? ArtworkFromImagesSkipReason.NoWebAddress
        : null;
}
