using ArtistShop.Web.Database;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

// The bulk image upload's files matched to works by name, against name matches and file names
// as the database would give them
public sealed class BulkImagePlannerTests
{
    private static readonly WorkId Dawn = new(7);

    private static WorkNameMatch Match(WorkNameMatchType type, params WorkId[] ids) => new(type, ids);

    private static readonly WorkNameMatch Nothing = Match(WorkNameMatchType.NoWork);

    private static BulkImageTarget Target(
        Dictionary<string, WorkNameMatch> matches,
        params (WorkId WorkId, string Name)[] fileNames
    ) =>
        new(
            new Dictionary<string, WorkNameMatch>(matches, DatabaseCollationComparer.Instance),
            fileNames.ToLookup(entry => entry.WorkId, entry => entry.Name)
        );

    private static IReadOnlyList<BulkImageCandidate> Files(params string[] paths) =>
        [.. paths.Select(path => new BulkImageCandidate(path, $"/Painting/{path}"))];

    private static Dictionary<string, BulkImagePlanItem> PlanById(IReadOnlyList<BulkImageCandidate> files, BulkImageTarget target) =>
        BulkImagePlanner.Plan(files, target).ToDictionary(item => item.Id);

    [Fact]
    public void ANumberedFileIsAnotherImageOfTheWorkBeforeTheBrackets()
    {
        var plan = PlanById(
            Files("Dawn.jpg", "Dawn (2).jpg", "Dawn (-1).png"),
            Target(new() { ["Dawn"] = Match(WorkNameMatchType.OneImagelessWork, Dawn), ["Dawn (2)"] = Nothing, ["Dawn (-1)"] = Nothing })
        );

        Assert.Equal(BulkImageOutcome.WillAttach, plan["Dawn.jpg"].Outcome);
        Assert.Null(plan["Dawn.jpg"].ExtraNumber);

        var second = plan["Dawn (2).jpg"];
        Assert.Equal((BulkImageOutcome.WillAddExtra, Dawn.Value), (second.Outcome, second.WorkIds[0]));
        Assert.Equal(2, second.ExtraNumber);

        Assert.Equal(BulkImageOutcome.WillAddExtra, plan["Dawn (-1).png"].Outcome);
        Assert.Equal(-1, plan["Dawn (-1).png"].ExtraNumber);
    }

    // an extra image is added whether or not the work has images already
    [Fact]
    public void AWorkWithImagesStillTakesAnotherImage()
    {
        var plan = PlanById(
            Files("Dawn (3).jpg"),
            Target(new() { ["Dawn"] = Match(WorkNameMatchType.WorkWithImages, Dawn), ["Dawn (3)"] = Nothing })
        );

        Assert.Equal(BulkImageOutcome.WillAddExtra, plan["Dawn (3).jpg"].Outcome);
    }

    [Fact]
    public void AWorkTitledWithTheBracketsTakesItsOwnFile()
    {
        var titled = new WorkId(9);
        var plan = PlanById(
            Files("Dawn (2).jpg"),
            Target(new() { ["Dawn (2)"] = Match(WorkNameMatchType.OneImagelessWork, titled), ["Dawn"] = Match(WorkNameMatchType.OneImagelessWork, Dawn) })
        );

        Assert.Equal((BulkImageOutcome.WillAttach, titled.Value), (plan["Dawn (2).jpg"].Outcome, plan["Dawn (2).jpg"].WorkIds[0]));
    }

    // so uploading the same folder twice adds its other images once
    [Fact]
    public void AFileTheWorkAlreadyHasAnImageFromIsLeftOut()
    {
        var plan = PlanById(
            Files("Dawn (2).jpg"),
            Target(new() { ["Dawn"] = Match(WorkNameMatchType.WorkWithImages, Dawn), ["Dawn (2)"] = Nothing }, (Dawn, "dawn (2).JPG"))
        );

        Assert.Equal(BulkImageOutcome.WorkHasThisFile, plan["Dawn (2).jpg"].Outcome);
    }

    [Fact]
    public void AnExtraImageOfNoWorkOrOfSeveralIsLeftOut()
    {
        var plan = PlanById(
            Files("Dusk (2).jpg", "Twin (2).jpg"),
            Target(
                new()
                {
                    ["Dusk (2)"] = Nothing,
                    ["Dusk"] = Nothing,
                    ["Twin (2)"] = Nothing,
                    ["Twin"] = Match(WorkNameMatchType.SeveralWorks, new WorkId(1), new WorkId(2)),
                }
            )
        );

        Assert.Equal(BulkImageOutcome.NoWork, plan["Dusk (2).jpg"].Outcome);
        Assert.Equal(BulkImageOutcome.SeveralWorks, plan["Twin (2).jpg"].Outcome);
    }

    [Fact]
    public void TheSameNameTwiceInOneUploadUsesNeither()
    {
        var plan = PlanById(
            Files("Dawn (2).jpg", "dawn (2).png"),
            Target(new() { ["Dawn (2)"] = Nothing, ["Dawn"] = Match(WorkNameMatchType.WorkWithImages, Dawn) })
        );

        Assert.All(plan.Values, item => Assert.Equal(BulkImageOutcome.DuplicateNameInUpload, item.Outcome));
    }

    [Fact]
    public void ItLooksUpEachNameAndEachExtraImagesWorkOnce()
    {
        var names = BulkImagePlanner.NamesToMatch(Files("Dawn.jpg", "Dawn (2).jpg", "dawn (3).jpg", "Study 2.jpg"));

        Assert.Equal(["Dawn", "Dawn (2)", "dawn (3)", "Study 2"], names.Select(name => name.Value));
    }

    [Fact]
    public void OnlyTheWorksOfExtraImagesHaveTheirFileNamesChecked()
    {
        var files = Files("Dawn.jpg", "Dawn (2).jpg");
        var matches = new Dictionary<string, WorkNameMatch>(DatabaseCollationComparer.Instance)
        {
            ["Dawn"] = Match(WorkNameMatchType.OneImagelessWork, Dawn),
            ["Dawn (2)"] = Nothing,
        };

        Assert.Equal([Dawn], BulkImagePlanner.WorksOfExtraImages(files, matches));
    }
}
