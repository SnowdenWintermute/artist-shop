CREATE OR ALTER PROCEDURE dbo.GetPaintingBySlug @Slug nvarchar(200) AS BEGIN
SET
NOCOUNT ON;

SELECT
    shopItem.Id,
    shopItem.Name,
    shopItem.Slug,
    shopItem.Price,
    shopItem.Stock,
    painting.DatePainted,
    painting.DatePaintedPrecision,
    painting.Description,
    painting.WidthCm,
    painting.HeightCm
FROM
    dbo.Paintings AS painting
    JOIN dbo.ShopItems AS shopItem ON shopItem.Id = painting.Id
WHERE
    shopItem.Slug = @Slug;

SELECT
    shopItemImages.RelativePath,
    shopItemImages.OriginalFileName,
    shopItemImages.IsPrimary,
    shopItemImages.Width,
    shopItemImages.Height,
    shopItemImages.BlurDataUri
FROM
    dbo.ShopItemImages AS shopItemImages
    JOIN dbo.ShopItems shopItem ON shopItem.Id = shopItemImages.ShopItemId
WHERE
    shopItem.Slug = @Slug
ORDER BY
    shopItemImages.SortOrder;

SELECT
    series.Id,
    series.Name,
    series.Slug
FROM
    dbo.Series AS series
    JOIN dbo.ShopItemAndSeriesJunction AS junction ON junction.SeriesId = series.Id
    JOIN dbo.ShopItems AS shopItem ON shopItem.Id = junction.ShopItemId
WHERE
    shopItem.Slug = @Slug;

-- result set 4: this painting's terms, each with its vocabulary so the page can
-- show "Medium: Acrylic" without another query
SELECT
    term.Id,
    term.Name,
    vocabulary.Id AS VocabularyId,
    vocabulary.Name AS VocabularyName
FROM
    dbo.ShopItemAndVocabularyTermsJunction AS junction
    JOIN dbo.VocabularyTerms AS term ON term.Id = junction.TermId
    JOIN dbo.Vocabularies AS vocabulary ON vocabulary.Id = junction.VocabularyId
    JOIN dbo.ShopItems AS shopItem ON shopItem.Id = junction.ShopItemId
WHERE
    shopItem.Slug = @Slug;

END;
