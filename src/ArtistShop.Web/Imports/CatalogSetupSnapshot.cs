using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Imports;

// a vocabulary with the work types it applies to and its terms
public record VocabularySetup(
    VocabularyId Id,
    VocabularyName Name,
    bool IsMutuallyExclusive,
    IReadOnlyList<WorkTypeId> WorkTypeIds,
    IReadOnlyList<VocabularyTerm> Terms
);

// What a website's works are described with: the fields there are, the work types, and the
// vocabularies. The export writes it out, and the type and vocabulary imports plan against it
public record CatalogSetupSnapshot(
    IReadOnlyList<WorkFieldDefinition> Fields,
    IReadOnlyList<WorkTypeWithFields> Types,
    IReadOnlyList<VocabularySetup> Vocabularies
)
{
    public static async Task<CatalogSetupSnapshot> LoadAsync(
        WorkFieldRepository workFieldRepository,
        WorkTypeRepository workTypeRepository,
        VocabularyRepository vocabularyRepository
    )
    {
        var types = new List<WorkTypeWithFields>();
        var typeIdsByVocabulary = new Dictionary<VocabularyId, List<WorkTypeId>>();

        // in id order, since the repositories return lists unordered and a plan's fingerprint includes
        // a vocabulary's type ids: the same catalog read twice must plan the same
        foreach (var type in (await workTypeRepository.GetAllAsync()).OrderBy(type => type.Id.Value))
        {
            // null when the type was deleted since the list was read
            if (await workTypeRepository.GetAsync(type.Id) is not { } withFields)
            {
                continue;
            }

            types.Add(withFields);

            foreach (var vocabulary in await vocabularyRepository.GetAllWithTermsForWorkTypeAsync(type.Id))
            {
                typeIdsByVocabulary.TryAdd(vocabulary.Id, []);
                typeIdsByVocabulary[vocabulary.Id].Add(type.Id);
            }
        }

        var vocabularies = (await vocabularyRepository.GetAllWithTermsAsync())
            .OrderBy(vocabulary => vocabulary.Id.Value)
            .Select(vocabulary => new VocabularySetup(
                vocabulary.Id,
                vocabulary.Name,
                vocabulary.IsMutuallyExclusive,
                typeIdsByVocabulary.GetValueOrDefault(vocabulary.Id) ?? [],
                vocabulary.Terms
            ))
            .ToList();

        return new CatalogSetupSnapshot(await workFieldRepository.GetAllAsync(), types, vocabularies);
    }

    public IEnumerable<VocabularySetup> VocabulariesFor(WorkTypeId typeId) =>
        Vocabularies.Where(vocabulary => vocabulary.WorkTypeIds.Contains(typeId));
}
