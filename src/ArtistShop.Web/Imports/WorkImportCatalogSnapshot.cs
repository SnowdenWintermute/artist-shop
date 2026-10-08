using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Imports;

// a copy of the catalog data a plan checks rows against, read just before planning
public record WorkImportCatalogSnapshot(
    WorkTypeWithFields WorkType,
    IReadOnlyList<VocabularyWithTerms> TypeVocabularies,
    IReadOnlyList<Vocabulary> AllVocabularies,
    IReadOnlyList<Collection> AllCollections,
    IReadOnlyList<ProductType> ProductTypes,
    // this work type's works only, the way TypeVocabularies is this type's vocabularies
    IReadOnlyList<WorkTitleAndSlug> TypeWorks
)
{
    // null when the work type doesn't exist
    public static async Task<WorkImportCatalogSnapshot?> LoadAsync(
        WorkTypeId workTypeId,
        WorkTypeRepository workTypeRepository,
        VocabularyRepository vocabularyRepository,
        CollectionRepository collectionRepository,
        ProductTypeRepository productTypeRepository,
        WorkRepository workRepository
    )
    {
        var workType = await workTypeRepository.GetAsync(workTypeId);

        if (workType is null)
        {
            return null;
        }

        return new WorkImportCatalogSnapshot(
            workType,
            await vocabularyRepository.GetAllWithTermsForWorkTypeAsync(workTypeId),
            await vocabularyRepository.GetAllAsync(),
            await collectionRepository.GetAllAsync(),
            await productTypeRepository.GetAllAsync(),
            await workRepository.GetTitlesOfTypeAsync(workTypeId)
        );
    }
}
