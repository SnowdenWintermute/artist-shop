CREATE OR ALTER PROCEDURE dbo.ReorderSeriesShopItems @SeriesId int,
@ShopItemIds dbo.OrderedIdList READONLY AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

SET
TRANSACTION ISOLATION LEVEL SERIALIZABLE;

BEGIN TRANSACTION;

-- the list must be exactly the series' shop items, or another tab changed them after this page
-- loaded. Its primary key rules out duplicates, so equal counts plus every id being a member
-- means the same set
IF (
    SELECT
        COUNT(*)
    FROM
        dbo.ShopItemAndSeriesJunction
    WHERE
        SeriesId = @SeriesId
) <> (
    SELECT
        COUNT(*)
    FROM
        @ShopItemIds
)
OR EXISTS (
    SELECT
        1
    FROM
        @ShopItemIds AS ordered
    WHERE
        NOT EXISTS (
            SELECT
                1
            FROM
                dbo.ShopItemAndSeriesJunction AS junction
            WHERE
                junction.SeriesId = @SeriesId
                AND junction.ShopItemId = ordered.Id
        )
) THROW 50005,
'The series has changed since the page loaded.',
1;

-- one statement: SQL Server checks UNIQUE (SeriesId, SortOrder) against the finished update,
-- so two rows can swap positions. Row-by-row updates would collide halfway
UPDATE junction
SET
    junction.SortOrder = ordered.SortOrder
FROM
    dbo.ShopItemAndSeriesJunction AS junction
    JOIN @ShopItemIds AS ordered ON ordered.Id = junction.ShopItemId
WHERE
    junction.SeriesId = @SeriesId;

COMMIT TRANSACTION;

END;
