using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Imports;

// a vocabulary with the artwork types it applies to and its terms
public record VocabularySetup(
    VocabularyId Id,
    VocabularyName Name,
    bool IsSingleChoice,
    IReadOnlyList<ArtworkTypeId> ArtworkTypeIds,
    IReadOnlyList<VocabularyTerm> Terms
);

// What a website's artworks are described with: the fields there are, the artwork types, and the
// vocabularies. The export writes it out, and the type and vocabulary imports plan against it
public record CatalogSetupSnapshot(
    IReadOnlyList<ArtworkFieldDefinition> Fields,
    IReadOnlyList<ArtworkTypeWithFields> Types,
    IReadOnlyList<VocabularySetup> Vocabularies
)
{
    public static async Task<CatalogSetupSnapshot> LoadAsync(
        ArtworkFieldRepository artworkFieldRepository,
        ArtworkTypeRepository artworkTypeRepository,
        VocabularyRepository vocabularyRepository
    )
    {
        var types = new List<ArtworkTypeWithFields>();
        var typeIdsByVocabulary = new Dictionary<VocabularyId, List<ArtworkTypeId>>();

        // in id order, since the repositories return lists unordered and a plan's fingerprint includes
        // a vocabulary's type ids: the same catalog read twice must plan the same
        foreach (var type in (await artworkTypeRepository.GetAllAsync()).OrderBy(type => type.Id.Value))
        {
            // null when the type was deleted since the list was read
            if (await artworkTypeRepository.GetAsync(type.Id) is not { } withFields)
            {
                continue;
            }

            types.Add(withFields);

            foreach (var vocabulary in await vocabularyRepository.GetAllWithTermsForArtworkTypeAsync(type.Id))
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
                vocabulary.IsSingleChoice,
                typeIdsByVocabulary.GetValueOrDefault(vocabulary.Id) ?? [],
                vocabulary.Terms
            ))
            .ToList();

        return new CatalogSetupSnapshot(await artworkFieldRepository.GetAllAsync(), types, vocabularies);
    }

    public IEnumerable<VocabularySetup> VocabulariesFor(ArtworkTypeId typeId) =>
        Vocabularies.Where(vocabulary => vocabulary.ArtworkTypeIds.Contains(typeId));
}
