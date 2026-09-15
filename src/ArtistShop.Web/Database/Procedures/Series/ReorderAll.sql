CREATE OR ALTER PROCEDURE dbo.ReorderSeries @SeriesIds dbo.OrderedIdList READONLY AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

SET
TRANSACTION ISOLATION LEVEL SERIALIZABLE;

BEGIN TRANSACTION;

-- the list must be exactly the series that exist, or another tab added or deleted one after this
-- page loaded
IF (
    SELECT
        COUNT(*)
    FROM
        dbo.Series
) <> (
    SELECT
        COUNT(*)
    FROM
        @SeriesIds
)
OR EXISTS (
    SELECT
        1
    FROM
        @SeriesIds AS ordered
    WHERE
        NOT EXISTS (
            SELECT
                1
            FROM
                dbo.Series AS series
            WHERE
                series.Id = ordered.Id
        )
) THROW 50008,
'The series have changed since the page loaded.',
1;

UPDATE series
SET
    series.SortOrder = ordered.SortOrder
FROM
    dbo.Series AS series
    JOIN @SeriesIds AS ordered ON ordered.Id = series.Id;

COMMIT TRANSACTION;

END;
