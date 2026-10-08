using ArtistShop.Web.Exports;
using ArtistShop.Web.Imports;
using static ArtistShop.Web.Tests.Imports.CatalogSetupTestData;

namespace ArtistShop.Web.Tests.Exports;

public sealed class CatalogSetupCsvExportTests
{
    [Fact]
    public void TheTypeImportReadsBackEveryType()
    {
        var csv = CatalogSetupCsvExport.WorkTypes(Snapshot(), ';');

        var plan = WorkTypeImportPlanner.Plan(csv, ';', EmptySnapshot());

        Assert.Empty(plan.Errors);
        Assert.Equal([Painting.Name, Sculpture.Name], plan.Additions.Select(addition => addition.Name));
        Assert.Equal(Painting.Fields, plan.Additions[0].Fields);
        Assert.Equal(Sculpture.Fields, plan.Additions[1].Fields);
    }

    [Fact]
    public void TheVocabularyImportReadsBackEveryVocabulary()
    {
        var csv = CatalogSetupCsvExport.Vocabularies(Snapshot(), ';');

        // the types exist by then, as the README's order makes sure
        var plan = VocabularyImportPlanner.Plan(csv, ';', Snapshot() with { Vocabularies = [] });

        Assert.Empty(plan.Errors);
        var change = Assert.Single(plan.Changes);
        Assert.Equal(Medium.Name, change.Name);
        Assert.Equal(Medium.WorkTypeIds, change.AddedWorkTypes.Select(type => type.Id));
        Assert.Equal(Medium.Terms.Select(term => term.Name), change.AddedTerms);
    }

    [Fact]
    public void ListsAreWrittenWithTheSeparator()
    {
        var csv = CatalogSetupCsvExport.WorkTypes(Snapshot(), '|');

        Assert.Equal("workType,fields\r\nPainting,Date created| Height and width\r\nSculpture,Height and width| Depth\r\n", csv);
    }
}
