CREATE OR ALTER PROCEDURE dbo.AddSeries @Name nvarchar(256),
@Slug nvarchar(200) AS BEGIN
SET
NOCOUNT ON;

-- a slug another series has fails Unique_Series_Slug: unlike artworks, series slugs aren't numbered
-- New series go last. HOLDLOCK keeps the range read by MAX locked until the insert, and UPDLOCK
-- makes a second add wait here instead of reading the same MAX and failing Unique_Series_SortOrder
INSERT INTO
    dbo.Series (Name, Slug, SortOrder)
    OUTPUT INSERTED.Id
SELECT
    @Name,
    @Slug,
    COALESCE(MAX(SortOrder), -1) + 1
FROM
    dbo.Series
WITH
    (UPDLOCK, HOLDLOCK);

END;
