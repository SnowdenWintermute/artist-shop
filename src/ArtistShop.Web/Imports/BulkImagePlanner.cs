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
    NoWork = 3,
    SeveralWorks = 4,
    WorkHasImages = 5,
    DuplicateNameInUpload = 6,
    Failed = 7,

    // "Dawn (2)": another image of a work, after the ones it has
    WillAddExtra = 8,
    ExtraAdded = 9,
    WorkHasThisFile = 10,
}

// one image the page found: the id its script knows the file by, and its path in what was dropped
public record BulkImageCandidate(string Id, string Path);

// What happens to one file. WorkIds are the works its name matched, so the report can link to
// them; ExtraNumber is set for another image of a work, like "Dawn (2)", and orders it after the
// others
public record BulkImagePlanItem(
    string Id,
    string Path,
    WorkName WorkName,
    BulkImageOutcome Outcome,
    IReadOnlyList<int> WorkIds,
    int? ExtraNumber
);

// The works of the chosen type the files' names can go to. MatchesByName holds every name
// BulkImagePlanner.NamesToMatch gives, compared as the database compares names; FileNames holds
// the names the images of the works an extra image would go to were uploaded under
public record BulkImageTarget(IReadOnlyDictionary<string, WorkNameMatch> MatchesByName, ILookup<WorkId, string> FileNames)
{
    // Throws ChangedSincePageLoadException when the work type is gone
    public static async Task<BulkImageTarget> LoadAsync(
        WorkTypeId typeId,
        IReadOnlyList<BulkImageCandidate> candidates,
        WorkImageRepository workImageRepository
    )
    {
        var matches = (await workImageRepository.GetWorkNameMatchesAsync(typeId, BulkImagePlanner.NamesToMatch(candidates)))
            .ToDictionary(entry => entry.Key.Value, entry => entry.Value, DatabaseCollationComparer.Instance);
        var extraWorkIds = BulkImagePlanner.WorksOfExtraImages(candidates, matches);

        return new BulkImageTarget(matches, await workImageRepository.GetFileNamesAsync(extraWorkIds));
    }
}

// Matches the bulk image upload's files to works by name, without touching the database or the
// files. "Dawn.jpg" is Dawn's first image when Dawn has none; "Dawn (2).jpg" is another image of
// Dawn, after the ones it has, unless a work is titled "Dawn (2)" or Dawn already has an image
// uploaded under that file name, so uploading a folder twice adds nothing twice
public static class BulkImagePlanner
{
    private static readonly ILookup<WorkId, string> NoFileNames = Array.Empty<(WorkId WorkId, string Name)>()
        .ToLookup(entry => entry.WorkId, entry => entry.Name);

    // each file's own name, and for "Dawn (2)" also "Dawn", once each as the database compares them
    public static IReadOnlyList<WorkName> NamesToMatch(IEnumerable<BulkImageCandidate> candidates)
    {
        var names = Matchable(candidates).ToList();

        return
        [
            .. names
                .Concat(names.Select(name => name.AsExtraImage()?.Work).OfType<WorkName>())
                .Select(name => name.Value)
                .Distinct(DatabaseCollationComparer.Instance)
                .Select(name => new WorkName(name)),
        ];
    }

    // the works extra images would go to, whose images' file names the plan then checks: the plan
    // without them
    public static IReadOnlyList<WorkId> WorksOfExtraImages(
        IEnumerable<BulkImageCandidate> candidates,
        IReadOnlyDictionary<string, WorkNameMatch> matchesByName
    ) =>
        [
            .. Plan(candidates, new BulkImageTarget(matchesByName, NoFileNames))
                .Where(item => item.Outcome is BulkImageOutcome.WillAddExtra)
                .Select(item => new WorkId(item.WorkIds[0]))
                .Distinct(),
        ];

    public static IReadOnlyList<BulkImagePlanItem> Plan(IEnumerable<BulkImageCandidate> candidates, BulkImageTarget target)
    {
        var files = candidates.Select(candidate => (Candidate: candidate, Name: WorkName.FromFileName(candidate.Path))).ToList();
        var items = new Dictionary<string, BulkImagePlanItem>();

        foreach (var (candidate, name) in files.Where(file => !file.Name.CanMatchAnWork))
        {
            items[candidate.Id] = new BulkImagePlanItem(candidate.Id, candidate.Path, name, BulkImageOutcome.NoWork, [], ExtraNumber: null);
        }

        // one group per name as the database compares them: "Sunset.jpg" and "sunset.png" are one
        // name and neither is used
        foreach (var sameName in files.Where(file => file.Name.CanMatchAnWork).GroupBy(file => file.Name.Value, DatabaseCollationComparer.Instance))
        {
            var match = target.MatchesByName[sameName.Key];
            var isRepeated = sameName.Count() > 1;

            // a work with this exact title takes the file, even one named like another image
            if (match.Type is not WorkNameMatchType.NoWork || sameName.First().Name.AsExtraImage() is not { } extra)
            {
                var outcome = isRepeated ? BulkImageOutcome.DuplicateNameInUpload : OutcomeOf(match.Type);
                AddAll(sameName, outcome, match, extraNumber: null);
                continue;
            }

            var workMatch = target.MatchesByName[extra.Work.Value];
            var extraOutcome =
                isRepeated ? BulkImageOutcome.DuplicateNameInUpload
                : workMatch.Type is WorkNameMatchType.NoWork ? BulkImageOutcome.NoWork
                : workMatch.Type is WorkNameMatchType.SeveralWorks ? BulkImageOutcome.SeveralWorks
                : target.FileNames[workMatch.WorkIds[0]].Contains(FileNameOf(sameName.First().Candidate.Path), StringComparer.OrdinalIgnoreCase)
                    ? BulkImageOutcome.WorkHasThisFile
                : BulkImageOutcome.WillAddExtra;

            AddAll(sameName, extraOutcome, workMatch, extra.Number);
        }

        return [.. files.Select(file => items[file.Candidate.Id])];

        void AddAll(IEnumerable<(BulkImageCandidate Candidate, WorkName Name)> sameName, BulkImageOutcome outcome, WorkNameMatch match, int? extraNumber)
        {
            foreach (var (candidate, name) in sameName)
            {
                items[candidate.Id] = new BulkImagePlanItem(
                    candidate.Id,
                    candidate.Path,
                    name,
                    outcome,
                    [.. match.WorkIds.Select(workId => workId.Value)],
                    extraNumber
                );
            }
        }
    }

    // the outcome the server's answer for a first image means, as the plan's is
    public static BulkImageOutcome OutcomeOf(WorkNameMatchType matchType) =>
        matchType switch
        {
            WorkNameMatchType.OneImagelessWork => BulkImageOutcome.WillAttach,
            WorkNameMatchType.NoWork => BulkImageOutcome.NoWork,
            WorkNameMatchType.SeveralWorks => BulkImageOutcome.SeveralWorks,
            WorkNameMatchType.WorkWithImages => BulkImageOutcome.WorkHasImages,
        };

    private static IEnumerable<WorkName> Matchable(IEnumerable<BulkImageCandidate> candidates) =>
        candidates.Select(candidate => WorkName.FromFileName(candidate.Path)).Where(name => name.CanMatchAnWork);

    // what the upload stores as the image's file name
    private static string FileNameOf(string path) => Path.GetFileName(path.Trim('/'));
}
