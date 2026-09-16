CREATE OR ALTER PROCEDURE dbo.ClearSeriesCover @SeriesId int AS BEGIN
SET
NOCOUNT ON;

-- with no starred artwork, the cover falls back to the first one in order with an image
UPDATE dbo.ArtworkAndSeriesJunction
SET
    IsCover = 0
WHERE
    SeriesId = @SeriesId
    AND IsCover = 1;

END;
