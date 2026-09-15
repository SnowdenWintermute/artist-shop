CREATE OR ALTER PROCEDURE dbo.GetShopItemTypes AS BEGIN
SET
NOCOUNT ON;

SELECT
    shopItemType.Id,
    shopItemType.Name
FROM
    dbo.ShopItemTypes AS shopItemType;

END;
