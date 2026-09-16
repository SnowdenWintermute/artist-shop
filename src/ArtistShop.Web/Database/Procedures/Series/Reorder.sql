CREATE OR ALTER PROCEDURE dbo.ReorderSeriesArtworks @SeriesId int,
@ArtworkIds dbo.OrderedIdList READONLY AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

SET
TRANSACTION ISOLATION LEVEL SERIALIZABLE;

BEGIN TRANSACTION;

-- the list must be exactly the series' artworks, or another tab changed them after this page
-- loaded. Its primary key rules out duplicates, so equal counts plus every id being a member
-- means the same set
IF (
    SELECT
        COUNT(*)
    FROM
        dbo.ArtworkAndSeriesJunction
    WHERE
        SeriesId = @SeriesId
) <> (
    SELECT
        COUNT(*)
    FROM
        @ArtworkIds
)
OR EXISTS (
    SELECT
        1
    FROM
        @ArtworkIds AS ordered
    WHERE
        NOT EXISTS (
            SELECT
                1
            FROM
                dbo.ArtworkAndSeriesJunction AS junction
            WHERE
                junction.SeriesId = @SeriesId
                AND junction.ArtworkId = ordered.Id
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
    dbo.ArtworkAndSeriesJunction AS junction
    JOIN @ArtworkIds AS ordered ON ordered.Id = junction.ArtworkId
WHERE
    junction.SeriesId = @SeriesId;

COMMIT TRANSACTION;

END;
