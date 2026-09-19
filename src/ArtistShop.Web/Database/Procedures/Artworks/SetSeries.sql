CREATE OR ALTER PROCEDURE dbo.SetArtworkSeries @ArtworkId int,
@SeriesIds dbo.IdList READONLY AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

-- A series row does carry something of its own: SortOrder and IsCover. So unticked rows are deleted
-- and newly ticked ones appended, leaving the rest where the artist dragged them. Dropping a row
-- takes IsCover with it, which is what removing a series' cover artwork from that series means.
DELETE junction
FROM
    dbo.ArtworkAndSeriesJunction AS junction
WHERE
    junction.ArtworkId = @ArtworkId
    AND NOT EXISTS (
        SELECT
            1
        FROM
            @SeriesIds AS seriesIds
        WHERE
            seriesIds.Id = junction.SeriesId
    );

INSERT INTO
    dbo.ArtworkAndSeriesJunction (ArtworkId, SeriesId, SortOrder)
SELECT
    @ArtworkId,
    seriesIds.Id,
    -- a correlated subquery: it runs once per row of @SeriesIds, with seriesIds.Id
    -- filled in from the outer row. MAX over no rows is NULL, so COALESCE turns
    -- "empty series" into -1, which the + 1 makes 0
    COALESCE(
        (
            SELECT
                MAX(existing.SortOrder)
            FROM
                dbo.ArtworkAndSeriesJunction AS existing
            WITH
                (UPDLOCK)
            WHERE
                existing.SeriesId = seriesIds.Id
        ),
        -1
    ) + 1
FROM
    @SeriesIds AS seriesIds
WHERE
    NOT EXISTS (
        SELECT
            1
        FROM
            dbo.ArtworkAndSeriesJunction AS junction
        WHERE
            junction.ArtworkId = @ArtworkId
            AND junction.SeriesId = seriesIds.Id
    );

END;
