CREATE OR ALTER PROCEDURE dbo.GetSeries @Id int AS BEGIN
SET
NOCOUNT ON;

SELECT
    Id,
    Name,
    Slug
FROM
    dbo.Series
WHERE
    Id = @Id;

SELECT
    shopItem.Id,
    shopItem.Name,
    shopItemType.Name AS ShopItemTypeName,
    junction.IsCover,
    primaryImage.RelativePath,
    primaryImage.OriginalFileName,
    primaryImage.Width,
    primaryImage.Height,
    primaryImage.BlurDataUri
FROM
    dbo.ShopItemAndSeriesJunction AS junction
    JOIN dbo.ShopItems AS shopItem ON shopItem.Id = junction.ShopItemId
    JOIN dbo.ShopItemTypes AS shopItemType ON shopItemType.Id = shopItem.ShopItemTypeId
    LEFT JOIN dbo.ShopItemImages AS primaryImage ON primaryImage.ShopItemId = shopItem.Id
    AND primaryImage.IsPrimary = 1
WHERE
    junction.SeriesId = @Id
ORDER BY
    junction.SortOrder;

END;
