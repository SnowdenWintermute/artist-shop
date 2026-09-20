CREATE OR ALTER PROCEDURE dbo.GetSeriesBySlug @Slug nvarchar(200) AS BEGIN
SET
NOCOUNT ON;

SELECT
    Id,
    Name,
    Slug
FROM
    dbo.Series
WHERE
    Slug = @Slug;

END;
