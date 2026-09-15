CREATE OR ALTER PROCEDURE dbo.SetSeriesCover @SeriesId int,
@ShopItemId int AS BEGIN
SET
NOCOUNT ON;

IF NOT EXISTS (
    SELECT
        1
    FROM
        dbo.ShopItemAndSeriesJunction
    WHERE
        SeriesId = @SeriesId
        AND ShopItemId = @ShopItemId
) THROW 50006,
'The shop item is no longer in the series.',
1;

IF NOT EXISTS (
    SELECT
        1
    FROM
        dbo.ShopItemImages
    WHERE
        ShopItemId = @ShopItemId
        AND IsPrimary = 1
) THROW 50007,
'The shop item has no image to use as the cover.',
1;

-- one statement moves the star, so the filtered unique index never sees two covers
UPDATE dbo.ShopItemAndSeriesJunction
SET
    IsCover = IIF(ShopItemId = @ShopItemId, 1, 0)
WHERE
    SeriesId = @SeriesId;

END;
