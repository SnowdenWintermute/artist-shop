CREATE OR ALTER PROCEDURE dbo.AddManySeries @Series dbo.SeriesNameAndSlugList READONLY AS BEGIN
SET
NOCOUNT ON;

-- a name or slug another series has fails Unique_Series_Name or Unique_Series_Slug: unlike
-- artworks, series slugs aren't numbered
-- New series go last. Every row of one INSERT reads the same MAX, so ROW_NUMBER spreads them out
-- rather than all of them asking for MAX + 1 and failing Unique_Series_SortOrder. HOLDLOCK keeps
-- the range read by MAX locked until the insert, and UPDLOCK makes a second add wait here instead
-- of reading the same MAX
INSERT INTO
    dbo.Series (Name, Slug, SortOrder)
    OUTPUT INSERTED.Id,
    INSERTED.Name
SELECT
    series.Name,
    series.Slug,
    (
        SELECT
            COALESCE(MAX(SortOrder), -1)
        FROM
            dbo.Series
        WITH
            (UPDLOCK, HOLDLOCK)
    ) + ROW_NUMBER() OVER (
        ORDER BY
            series.Name
    )
FROM
    @Series AS series;

END;
