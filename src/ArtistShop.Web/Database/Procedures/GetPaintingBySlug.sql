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
    medium.Id,
    medium.Name
FROM
    dbo.Mediums AS medium
    JOIN dbo.PaintingAndMediumsJunction AS junction ON junction.MediumId = medium.Id
    JOIN dbo.ShopItems AS shopItem ON shopItem.Id = junction.PaintingId
WHERE
    shopItem.Slug = @Slug;

SELECT
    support.Id,
    support.Name
FROM
    dbo.Supports AS support
    JOIN dbo.PaintingAndSupportsJunction AS junction ON junction.SupportId = support.Id
    JOIN dbo.ShopItems AS shopItem ON shopItem.Id = junction.PaintingId
WHERE
    shopItem.Slug = @Slug;

SELECT
    series.Id,
    series.Name
FROM
    dbo.PaintingSeries AS series
    JOIN dbo.PaintingAndSeriesJunction AS junction ON junction.SeriesId = series.Id
    JOIN dbo.ShopItems AS shopItem ON shopItem.Id = junction.PaintingId
WHERE
    shopItem.Slug = @Slug;

END;
