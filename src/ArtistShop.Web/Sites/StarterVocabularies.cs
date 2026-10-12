namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;

// Examples a new site starts with, so the artist sees what vocabularies are for before making
// their own. Added when a site is made rather than by a migration, which would reach every
// existing site too
public static class StarterVocabularies
{
    private const string PaintingTypeName = "Painting";

    private static readonly (string Name, string[] Terms)[] PaintingVocabularies =
    [
        ("Medium", ["Oil", "Watercolor", "Acrylic"]),
        ("Support", ["Canvas", "Paper"]),
    ];

    public static async Task AddAsync(SiteDatabase database)
    {
        var workTypes = await new WorkTypeRepository(database).GetAllAsync();
        // seeded by the first migration
        var painting = Unwrap.Value(workTypes.Find(type => type.Name.Value == PaintingTypeName));
        var vocabularies = new VocabularyRepository(database);
        var terms = new VocabularyTermRepository(database);

        foreach (var (name, termNames) in PaintingVocabularies)
        {
            var vocabularyId = await vocabularies.AddAsync(new VocabularyName(name), isMutuallyExclusive: false, [painting.Id]);

            foreach (var termName in termNames)
            {
                await terms.AddAsync(vocabularyId, new VocabularyTermName(termName));
            }
        }
    }
}
