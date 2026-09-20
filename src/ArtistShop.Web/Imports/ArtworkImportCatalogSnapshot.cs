using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Imports;

// a copy of the catalog data a plan checks rows against, read just before planning
public record ArtworkImportCatalogSnapshot(
    ArtworkTypeWithFields ArtworkType,
    IReadOnlyList<VocabularyWithTerms> TypeVocabularies,
    IReadOnlyList<Vocabulary> AllVocabularies,
    IReadOnlyList<Series> AllSeries,
    IReadOnlyList<ProductType> ProductTypes,
    // this work type's titles only, the way TypeVocabularies is this type's vocabularies
    IReadOnlyList<string> TypeArtworkNames
)
{
    // null when the artwork type doesn't exist
    public static async Task<ArtworkImportCatalogSnapshot?> LoadAsync(
        ArtworkTypeId artworkTypeId,
        ArtworkTypeRepository artworkTypeRepository,
        VocabularyRepository vocabularyRepository,
        SeriesRepository seriesRepository,
        ProductTypeRepository productTypeRepository,
        ArtworkRepository artworkRepository
    )
    {
        var artworkType = await artworkTypeRepository.GetAsync(artworkTypeId);

        if (artworkType is null)
        {
            return null;
        }

        return new ArtworkImportCatalogSnapshot(
            artworkType,
            await vocabularyRepository.GetAllWithTermsForArtworkTypeAsync(artworkTypeId),
            await vocabularyRepository.GetAllAsync(),
            await seriesRepository.GetAllAsync(),
            await productTypeRepository.GetAllAsync(),
            await artworkRepository.GetNamesOfTypeAsync(artworkTypeId)
        );
    }
}
