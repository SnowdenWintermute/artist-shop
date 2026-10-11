using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

// the checks on a whole-website folder that stop both the review and the import
public sealed class WebsiteImportPlannerTests
{
    private static readonly ProductType Original = new(new ProductTypeId(31), new ProductTypeName("Original"), IsDefault: true);

    private static readonly WebsiteImportFolder EveryFile = new(
        """
        {
          "formatVersion": 3,
          "listSeparator": ";",
          "workFiles": [ { "file": "Painting.csv", "workType": "Painting" } ]
        }
        """,
        WorkTypesCsv: "",
        VocabulariesCsv: "",
        new Dictionary<string, string> { ["Painting.csv"] = "" },
        ImageList: "",
        new Dictionary<string, string>(),
        Posts: [],
        new LookImportFolder(
            """
            {
              "formatVersion": 1,
              "wording": {
                "collection": { "singular": null, "plural": null, "keepsCase": false },
                "work": { "singular": null, "plural": null, "keepsCase": false }
              },
              "themeInUse": { "preset": "Paper" },
              "themes": [],
              "brandImages": []
            }
            """,
            new Dictionary<string, string>()
        )
    );

    [Fact]
    public void AFolderWithEveryFileIsFine()
    {
        Assert.True(WebsiteImportPlanner.TryCheckFolder(EveryFile, [Original], out var manifest, out var look, out var problems));
        Assert.NotNull(manifest);
        Assert.NotNull(look);
        Assert.Empty(problems);
    }

    [Fact]
    public void EachMissingFileIsAProblem()
    {
        var folder = EveryFile with
        {
            WorkTypesCsv = null,
            ImageList = null,
            WorkCsvsByFileName = new Dictionary<string, string>(),
            Look = LookImportFolder.Empty,
        };

        Assert.False(WebsiteImportPlanner.TryCheckFolder(folder, [Original], out var manifest, out var look, out var problems));
        Assert.Null(manifest);
        Assert.Null(look);
        Assert.Equal(
            [
                "look-and-wording/look.json is missing.",
                "catalog/workTypes.csv is missing.",
                "catalog/works/Painting.csv is missing.",
                "images/images.csv is missing.",
            ],
            problems
        );
    }

    // every imported work gets a product of the website's default type
    [Fact]
    public void AWebsiteWithNoProductTypesCantTakeTheWorks()
    {
        Assert.False(WebsiteImportPlanner.TryCheckFolder(EveryFile, [], out _, out _, out var problems));
        Assert.Equal(["This website has no product types for the works' products."], problems);
    }

    [Fact]
    public void AnUnreadableLookIsAProblem()
    {
        var folder = EveryFile with { Look = new LookImportFolder("""{ "formatVersion": 1 }""", new Dictionary<string, string>()) };

        Assert.False(WebsiteImportPlanner.TryCheckFolder(folder, [Original], out _, out _, out var problems));
        Assert.Equal(["look-and-wording: Its look.json isn't readable."], problems);
    }
}
