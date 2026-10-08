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
          "formatVersion": 2,
          "listSeparator": ";",
          "workFiles": [ { "file": "Painting.csv", "workType": "Painting" } ],
          "wording": {
            "collection": { "singular": null, "plural": null, "keepsCase": false },
            "work": { "singular": null, "plural": null, "keepsCase": false }
          }
        }
        """,
        WorkTypesCsv: "",
        VocabulariesCsv: "",
        new Dictionary<string, string> { ["Painting.csv"] = "" },
        ImageList: "",
        new Dictionary<string, string>(),
        Posts: []
    );

    [Fact]
    public void AFolderWithEveryFileIsFine()
    {
        Assert.True(WebsiteImportPlanner.TryCheckFolder(EveryFile, [Original], out var manifest, out var problems));
        Assert.NotNull(manifest);
        Assert.Empty(problems);
    }

    [Fact]
    public void EachMissingFileIsAProblem()
    {
        var folder = EveryFile with { WorkTypesCsv = null, ImageList = null, WorkCsvsByFileName = new Dictionary<string, string>() };

        Assert.False(WebsiteImportPlanner.TryCheckFolder(folder, [Original], out var manifest, out var problems));
        Assert.Null(manifest);
        Assert.Equal(
            ["catalog/workTypes.csv is missing.", "catalog/works/Painting.csv is missing.", "images/images.csv is missing."],
            problems
        );
    }

    // every imported work gets a product of the website's default type
    [Fact]
    public void AWebsiteWithNoProductTypesCantTakeTheWorks()
    {
        Assert.False(WebsiteImportPlanner.TryCheckFolder(EveryFile, [], out _, out var problems));
        Assert.Equal(["This website has no product types for the works' products."], problems);
    }
}
