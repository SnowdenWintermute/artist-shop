CREATE OR ALTER PROCEDURE dbo.CountArtworkTypeArtworks @Id int AS BEGIN
SET
NOCOUNT ON;

-- COUNT(column) skips NULLs, so each counts the artworks with a value in that field
SELECT
    COUNT(*) AS Total,
    COUNT(DateCreated) AS WithDateCreated,
    COUNT(HeightCm) AS WithHeightAndWidth,
    COUNT(DepthCm) AS WithDepth,
    COUNT(DurationSeconds) AS WithDuration
FROM
    dbo.Artworks
WHERE
    ArtworkTypeId = @Id;

END;
