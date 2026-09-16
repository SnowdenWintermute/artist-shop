CREATE OR ALTER PROCEDURE dbo.SetSeriesCover @SeriesId int,
@ArtworkId int AS BEGIN
SET
NOCOUNT ON;

IF NOT EXISTS (
    SELECT
        1
    FROM
        dbo.ArtworkAndSeriesJunction
    WHERE
        SeriesId = @SeriesId
        AND ArtworkId = @ArtworkId
) THROW 50006,
'The artwork is no longer in the series.',
1;

IF NOT EXISTS (
    SELECT
        1
    FROM
        dbo.ArtworkImages
    WHERE
        ArtworkId = @ArtworkId
        AND IsPrimary = 1
) THROW 50007,
'The artwork has no image to use as the cover.',
1;

-- one statement moves the star, so the filtered unique index never sees two covers
UPDATE dbo.ArtworkAndSeriesJunction
SET
    IsCover = IIF(ArtworkId = @ArtworkId, 1, 0)
WHERE
    SeriesId = @SeriesId;

END;
