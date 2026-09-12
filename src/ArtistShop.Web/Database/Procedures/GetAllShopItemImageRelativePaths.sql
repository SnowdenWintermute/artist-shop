CREATE OR ALTER PROCEDURE dbo.GetAllShopItemImagesRelativePaths AS BEGIN
SET
NOCOUNT ON;

SELECT
    shopItemImages.RelativePath
FROM
    dbo.ShopItemImages AS shopItemImages;

END
