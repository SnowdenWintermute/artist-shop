using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Tests.Database;

public class CatalogTestData(SqlConnectionFactory connectionFactory)
{
    private readonly ArtworkTypeRepository _artworkTypes = new(connectionFactory);
    private readonly VocabularyRepository _vocabularies = new(connectionFactory);
    private readonly VocabularyTermRepository _terms = new(connectionFactory);
    private readonly ArtworkRepository _artworks = new(connectionFactory);
    private readonly SeriesRepository _series = new(connectionFactory);

    public async Task<ArtworkTypeId> GetTypeIdAsync(string name) =>
        (await _artworkTypes.GetAllAsync()).Single(type => type.Name.Value == name).Id;

    public Task<ArtworkTypeId> GetPaintingTypeIdAsync() => GetTypeIdAsync("Painting");

    public async Task<ArtworkIdentifiers> AddArtworkWithDimensionsAsync(
        ArtworkTypeId typeId,
        DimensionsCentimeters dimensions
    )
    {
        var name = $"Dimensions test {Guid.NewGuid():n}";

        return await _artworks.AddAsync(
            new ArtworkCatalogAddition(
                typeId,
                new ArtworkName(name),
                ArtworkSlug.FromName(name),
                Description: null,
                DateCreated: null,
                Dimensions: dimensions,
                Duration: null,
                Images: [],
                MainImageIndex: 0,
                VocabularyTermIds: [],
                SeriesIds: [],
                Products: []
            )
        );
    }

    public Task<ArtworkIdentifiers> AddArtworkAsync(ArtworkTypeId typeId, string name) =>
        _artworks.AddAsync(
            CreateArtworkAddition(
                typeId,
                name,
                termIds: [],
                seriesIds: [],
                images: [],
                products: [],
                duration: null
            )
        );

    public async Task<VocabularyId> AddPaintingVocabularyAsync() =>
        await _vocabularies.AddAsync(
            new VocabularyName($"Medium {Guid.NewGuid():n}"),
            [await GetPaintingTypeIdAsync()]
        );

    public Task<VocabularyTermId> AddTermAsync(VocabularyId vocabularyId) =>
        _terms.AddAsync(vocabularyId, new VocabularyTermName($"Oil {Guid.NewGuid():n}"));

    public Task<SeriesId> AddSeriesAsync()
    {
        var name = $"Series {Guid.NewGuid():n}";
        return _series.AddAsync(new SeriesName(name), SeriesSlug.FromName(name));
    }

    // the path only has to be unique: nothing reads the file
    public static ArtworkImage CreateTestImage() =>
        new($"test/{Guid.NewGuid():n}", OriginalFileName: null, 800, 600, BlurDataUri: null);

    public async Task<ArtworkSlug> AddPaintingWithTermAsync(VocabularyTermId termId) =>
        (
            await AddPaintingAsync(
                $"Vocabulary test painting {Guid.NewGuid():n}",
                termIds: [termId],
                seriesIds: [],
                images: []
            )
        ).Slug;

    public async Task<ArtworkId> AddPaintingInSeriesAsync(
        SeriesId seriesId,
        IReadOnlyList<ArtworkImage> images
    ) =>
        (
            await AddPaintingAsync(
                $"Series test painting {Guid.NewGuid():n}",
                termIds: [],
                seriesIds: [seriesId],
                images
            )
        ).Id;

    public Task<ArtworkIdentifiers> AddPaintingAsync(
        string name,
        IReadOnlyList<VocabularyTermId> termIds,
        IReadOnlyList<SeriesId> seriesIds,
        IReadOnlyList<ArtworkImage> images
    ) => AddPaintingAsync(name, termIds, seriesIds, images, products: [], duration: null);

    public async Task<ArtworkIdentifiers> AddPaintingAsync(
        string name,
        IReadOnlyList<VocabularyTermId> termIds,
        IReadOnlyList<SeriesId> seriesIds,
        IReadOnlyList<ArtworkImage> images,
        IReadOnlyList<ProductAddition> products,
        TimeSpan? duration
    )
    {
        return await _artworks.AddAsync(
            CreateArtworkAddition(
                await GetPaintingTypeIdAsync(),
                name,
                termIds,
                seriesIds,
                images,
                products,
                duration
            )
        );
    }

    public static ArtworkCatalogAddition CreateArtworkAddition(
        ArtworkTypeId typeId,
        string name,
        IReadOnlyList<VocabularyTermId> termIds,
        IReadOnlyList<SeriesId> seriesIds,
        IReadOnlyList<ArtworkImage> images,
        IReadOnlyList<ProductAddition> products,
        TimeSpan? duration
    ) =>
        new(
            typeId,
            new ArtworkName(name),
            ArtworkSlug.FromName(name),
            Description: null,
            DateCreated: null,
            Dimensions: null,
            Duration: duration,
            Images: images,
            MainImageIndex: 0,
            VocabularyTermIds: termIds,
            SeriesIds: seriesIds,
            Products: products
        );
}
