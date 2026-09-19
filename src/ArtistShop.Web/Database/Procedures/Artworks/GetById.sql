CREATE OR ALTER PROCEDURE dbo.GetArtworkById @Id int AS BEGIN
SET
NOCOUNT ON;

SELECT
    artwork.Id,
    artwork.ArtworkTypeId,
    artworkType.Name AS ArtworkTypeName,
    artwork.Name,
    artwork.Slug,
    artwork.Description,
    artwork.DateCreated,
    artwork.DateCreatedPrecision,
    artwork.HeightCm,
    artwork.WidthCm,
    artwork.DepthCm,
    artwork.DurationSeconds
FROM
    dbo.Artworks AS artwork
    JOIN dbo.ArtworkTypes AS artworkType ON artworkType.Id = artwork.ArtworkTypeId
WHERE
    artwork.Id = @Id;

SELECT
    StorageKey,
    OriginalFileName,
    IsPrimary,
    Width,
    Height,
    BlurDataUri
FROM
    dbo.ArtworkImages
WHERE
    ArtworkId = @Id
ORDER BY
    SortOrder;

SELECT
    series.Id,
    series.Name,
    series.Slug
FROM
    dbo.Series AS series
    JOIN dbo.ArtworkAndSeriesJunction AS junction ON junction.SeriesId = series.Id
WHERE
    junction.ArtworkId = @Id;

-- this artwork's terms, each with its vocabulary so the page can
-- show "Medium: Acrylic" without another query
SELECT
    term.Id,
    term.Name,
    vocabulary.Id AS VocabularyId,
    vocabulary.Name AS VocabularyName
FROM
    dbo.ArtworkAndVocabularyTermsJunction AS junction
    JOIN dbo.VocabularyTerms AS term ON term.Id = junction.TermId
    JOIN dbo.Vocabularies AS vocabulary ON vocabulary.Id = junction.VocabularyId
WHERE
    junction.ArtworkId = @Id;

SELECT
    product.Id,
    product.ProductTypeId,
    productType.Name AS ProductTypeName,
    productType.IsDefault AS ProductTypeIsDefault,
    product.Label,
    product.Price,
    product.EditionSize,
    product.Stock
FROM
    dbo.Products AS product
    JOIN dbo.ProductTypes AS productType ON productType.Id = product.ProductTypeId
WHERE
    product.ArtworkId = @Id
ORDER BY
    product.Id;

END;
