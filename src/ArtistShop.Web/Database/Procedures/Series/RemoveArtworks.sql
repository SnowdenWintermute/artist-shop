CREATE OR ALTER PROCEDURE dbo.RemoveArtworksFromSeries @SeriesId int,
@ArtworkIds dbo.IdList READONLY AS BEGIN
SET
NOCOUNT ON;

-- leaves gaps in SortOrder, which is fine: only the order matters, and a reorder renumbers
DELETE junction
FROM
    dbo.ArtworkAndSeriesJunction AS junction
WHERE
    junction.SeriesId = @SeriesId
    AND junction.ArtworkId IN (
        SELECT
            Id
        FROM
            @ArtworkIds
    );

END;
