CREATE OR ALTER PROCEDURE dbo.RemoveShopItemsFromSeries @SeriesId int,
@ShopItemIds dbo.IdList READONLY AS BEGIN
SET
NOCOUNT ON;

-- leaves gaps in SortOrder, which is fine: only the order matters, and a reorder renumbers
DELETE junction
FROM
    dbo.ShopItemAndSeriesJunction AS junction
WHERE
    junction.SeriesId = @SeriesId
    AND junction.ShopItemId IN (
        SELECT
            Id
        FROM
            @ShopItemIds
    );

END;
