using ArtistShop.Web.Database;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Imports;

public enum ArtworkFromImagesSkipReason : byte
{
    TooDeep = 1,
    UnknownSeriesFolder = 2,
    SameNameInFolder = 3,
    TitleTooLong = 4,
    NoWebAddress = 5,
    ArtworkExists = 6,
}

// One artwork to create: its title, the series its folder named, and its images in order
public record PlannedArtworkFromImages(ArtworkName Name, SeriesId? SeriesId, IReadOnlyList<BulkImageCandidate> Images);

public record SkippedImage(string Path, ArtworkFromImagesSkipReason Reason);

public record ArtworksFromImagesPlan(IReadOnlyList<PlannedArtworkFromImages> Artworks, IReadOnlyList<SkippedImage> Skipped);

// Turns dropped images into new artworks, titled by file name the way the Upload images page
// matches them, so that page can still attach whatever this one didn't get to send
public static class ArtworksFromImagesPlanner
{
    // seriesForFolders is null when folders are only somewhere images are kept. Otherwise an image's
    // folder names its series, matched by web address as series names are kept unique. Images
    // directly in the dropped folder need no series, so that folder can be named anything
    public static ArtworksFromImagesPlan Plan(IEnumerable<BulkImageCandidate> candidates, IReadOnlyList<Series>? seriesForFolders)
    {
        var skipped = new List<SkippedImage>();
        var placed = new List<(BulkImageCandidate Candidate, string Folder, SeriesId? SeriesId)>();

        foreach (var candidate in candidates.OrderBy(candidate => candidate.Path, StringComparer.InvariantCultureIgnoreCase))
        {
            var folders = candidate.Path.Trim('/').Split('/')[..^1];

            if (seriesForFolders is null || folders.Length is 0)
            {
                placed.Add((candidate, string.Join('/', folders), null));
                continue;
            }

            if (folders.Length > 2)
            {
                skipped.Add(new SkippedImage(candidate.Path, ArtworkFromImagesSkipReason.TooDeep));
                continue;
            }

            var slug = SeriesSlug.FromName(folders[^1]);
            var series = seriesForFolders.FirstOrDefault(series => series.Slug == slug);

            if (series is null && folders.Length is 2)
            {
                skipped.Add(new SkippedImage(candidate.Path, ArtworkFromImagesSkipReason.UnknownSeriesFolder));
                continue;
            }

            placed.Add((candidate, string.Join('/', folders), series?.Id));
        }

        var artworks = new List<PlannedArtworkFromImages>();

        foreach (var folder in placed.GroupBy(file => file.Folder))
        {
            var named = folder.Select(file => (file.Candidate, file.SeriesId, Name: ArtworkName.FromFileName(file.Candidate.Path))).ToList();
            var sameNames = named.GroupBy(file => file.Name.Value, DatabaseCollationComparer.Instance).ToList();

            foreach (var repeated in sameNames.Where(group => group.Count() > 1).SelectMany(group => group))
            {
                skipped.Add(new SkippedImage(repeated.Candidate.Path, ArtworkFromImagesSkipReason.SameNameInFolder));
            }

            var single = sameNames.Where(group => group.Count() is 1).Select(group => group.Single()).ToList();

            // "Dawn (2)" is another image of Dawn when Dawn is in the same folder; otherwise it's a
            // title. Only a plain title takes extras, so "Dawn (2) (3)" can't join a row that is
            // itself Dawn's extra and vanish
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

                artworks.Add(new PlannedArtworkFromImages(file.Name, file.SeriesId, images));
            }
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
