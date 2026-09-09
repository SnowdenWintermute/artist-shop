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
    dbo.Paintings painting
    -- is it inner join?
    JOIN dbo.ShopItems shopItem ON shopItem.Id = painting.Id
WHERE
    shopItem.Slug = @Slug;

SELECT
    shopItemImages.Path,
    shopItemImages.IsPrimary
FROM
    dbo.ShopItemImages shopItemImages
    JOIN dbo.ShopItems shopItem ON shopItem.Id = shopItemImages.ShopItemId
WHERE
    shopItem.Slug = @Slug
ORDER BY
    shopItemImages.SortOrder;

END;
