CREATE OR ALTER PROCEDURE dbo.GetAllSeries AS BEGIN
SET
NOCOUNT ON;

SELECT
    Id,
    Name,
    Slug
FROM
    dbo.Series
ORDER BY
    SortOrder;

END;
