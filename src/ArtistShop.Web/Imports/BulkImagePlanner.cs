using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Imports;

// what would happen, or did happen, to one file of the bulk image upload. The plan sets all of
// these but Attached, ExtraAdded and Failed, which come from the server's answer once it's uploaded
public enum BulkImageOutcome : byte
{
    WillAttach = 1,
    Attached = 2,
    NoArtwork = 3,
    SeveralArtworks = 4,
    ArtworkHasImages = 5,
    DuplicateNameInUpload = 6,
    Failed = 7,

    // "Dawn (2)": another image of an artwork, after the ones it has
    WillAddExtra = 8,
    ExtraAdded = 9,
    ArtworkHasThisFile = 10,
}

// one image the page found: the id its script knows the file by, and its path in what was dropped
public record BulkImageCandidate(string Id, string Path);

// What happens to one file. ArtworkIds are the artworks its name matched, so the report can link to
// them; ExtraNumber is set for another image of an artwork, like "Dawn (2)", and orders it after the
// others
public record BulkImagePlanItem(
    string Id,
    string Path,
    ArtworkName ArtworkName,
    BulkImageOutcome Outcome,
    IReadOnlyList<int> ArtworkIds,
    int? ExtraNumber
);

// The artworks of the chosen type the files' names can go to. MatchesByName holds every name
// BulkImagePlanner.NamesToMatch gives, compared as the database compares names; FileNames holds
// the names the images of the artworks an extra image would go to were uploaded under
public record BulkImageTarget(IReadOnlyDictionary<string, ArtworkNameMatch> MatchesByName, ILookup<ArtworkId, string> FileNames)
{
    // Throws ChangedSincePageLoadException when the artwork type is gone
    public static async Task<BulkImageTarget> LoadAsync(
        ArtworkTypeId typeId,
        IReadOnlyList<BulkImageCandidate> candidates,
        ArtworkImageRepository artworkImageRepository
    )
    {
        var matches = (await artworkImageRepository.GetArtworkNameMatchesAsync(typeId, BulkImagePlanner.NamesToMatch(candidates)))
            .ToDictionary(entry => entry.Key.Value, entry => entry.Value, DatabaseCollationComparer.Instance);
        var extraArtworkIds = BulkImagePlanner.ArtworksOfExtraImages(candidates, matches);

        return new BulkImageTarget(matches, await artworkImageRepository.GetFileNamesAsync(extraArtworkIds));
    }
}

// Matches the bulk image upload's files to artworks by name, without touching the database or the
// files. "Dawn.jpg" is Dawn's first image when Dawn has none; "Dawn (2).jpg" is another image of
// Dawn, after the ones it has, unless an artwork is titled "Dawn (2)" or Dawn already has an image
// uploaded under that file name, so uploading a folder twice adds nothing twice
public static class BulkImagePlanner
{
    private static readonly ILookup<ArtworkId, string> NoFileNames = Array.Empty<(ArtworkId ArtworkId, string Name)>()
        .ToLookup(entry => entry.ArtworkId, entry => entry.Name);

    // each file's own name, and for "Dawn (2)" also "Dawn", once each as the database compares them
    public static IReadOnlyList<ArtworkName> NamesToMatch(IEnumerable<BulkImageCandidate> candidates)
    {
        var names = Matchable(candidates).ToList();

        return
        [
            .. names
                .Concat(names.Select(name => name.AsExtraImage()?.Artwork).OfType<ArtworkName>())
                .Select(name => name.Value)
                .Distinct(DatabaseCollationComparer.Instance)
                .Select(name => new ArtworkName(name)),
        ];
    }

    // the artworks extra images would go to, whose images' file names the plan then checks: the plan
    // without them
    public static IReadOnlyList<ArtworkId> ArtworksOfExtraImages(
        IEnumerable<BulkImageCandidate> candidates,
        IReadOnlyDictionary<string, ArtworkNameMatch> matchesByName
    ) =>
        [
            .. Plan(candidates, new BulkImageTarget(matchesByName, NoFileNames))
                .Where(item => item.Outcome is BulkImageOutcome.WillAddExtra)
                .Select(item => new ArtworkId(item.ArtworkIds[0]))
                .Distinct(),
        ];

    public static IReadOnlyList<BulkImagePlanItem> Plan(IEnumerable<BulkImageCandidate> candidates, BulkImageTarget target)
    {
        var files = candidates.Select(candidate => (Candidate: candidate, Name: ArtworkName.FromFileName(candidate.Path))).ToList();
        var items = new Dictionary<string, BulkImagePlanItem>();

        foreach (var (candidate, name) in files.Where(file => !file.Name.CanMatchAnArtwork))
        {
            items[candidate.Id] = new BulkImagePlanItem(candidate.Id, candidate.Path, name, BulkImageOutcome.NoArtwork, [], ExtraNumber: null);
        }

        // one group per name as the database compares them: "Sunset.jpg" and "sunset.png" are one
        // name and neither is used
        foreach (var sameName in files.Where(file => file.Name.CanMatchAnArtwork).GroupBy(file => file.Name.Value, DatabaseCollationComparer.Instance))
        {
            var match = target.MatchesByName[sameName.Key];
            var isRepeated = sameName.Count() > 1;

            // an artwork with this exact title takes the file, even one named like another image
            if (match.Type is not ArtworkNameMatchType.NoArtwork || sameName.First().Name.AsExtraImage() is not { } extra)
            {
                var outcome = isRepeated ? BulkImageOutcome.DuplicateNameInUpload : OutcomeOf(match.Type);
                AddAll(sameName, outcome, match, extraNumber: null);
                continue;
            }

            var artworkMatch = target.MatchesByName[extra.Artwork.Value];
            var extraOutcome =
                isRepeated ? BulkImageOutcome.DuplicateNameInUpload
                : artworkMatch.Type is ArtworkNameMatchType.NoArtwork ? BulkImageOutcome.NoArtwork
                : artworkMatch.Type is ArtworkNameMatchType.SeveralArtworks ? BulkImageOutcome.SeveralArtworks
                : target.FileNames[artworkMatch.ArtworkIds[0]].Contains(FileNameOf(sameName.First().Candidate.Path), StringComparer.OrdinalIgnoreCase)
                    ? BulkImageOutcome.ArtworkHasThisFile
                : BulkImageOutcome.WillAddExtra;

            AddAll(sameName, extraOutcome, artworkMatch, extra.Number);
        }

        return [.. files.Select(file => items[file.Candidate.Id])];

        void AddAll(IEnumerable<(BulkImageCandidate Candidate, ArtworkName Name)> sameName, BulkImageOutcome outcome, ArtworkNameMatch match, int? extraNumber)
        {
            foreach (var (candidate, name) in sameName)
            {
                items[candidate.Id] = new BulkImagePlanItem(
                    candidate.Id,
                    candidate.Path,
                    name,
                    outcome,
                    [.. match.ArtworkIds.Select(artworkId => artworkId.Value)],
                    extraNumber
                );
            }
        }
    }

    // the outcome the server's answer for a first image means, as the plan's is
    public static BulkImageOutcome OutcomeOf(ArtworkNameMatchType matchType) =>
        matchType switch
        {
            ArtworkNameMatchType.OneImagelessArtwork => BulkImageOutcome.WillAttach,
            ArtworkNameMatchType.NoArtwork => BulkImageOutcome.NoArtwork,
            ArtworkNameMatchType.SeveralArtworks => BulkImageOutcome.SeveralArtworks,
            ArtworkNameMatchType.ArtworkWithImages => BulkImageOutcome.ArtworkHasImages,
        };

    private static IEnumerable<ArtworkName> Matchable(IEnumerable<BulkImageCandidate> candidates) =>
        candidates.Select(candidate => ArtworkName.FromFileName(candidate.Path)).Where(name => name.CanMatchAnArtwork);

    // what the upload stores as the image's file name
    private static string FileNameOf(string path) => Path.GetFileName(path.Trim('/'));
}
