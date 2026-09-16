CREATE OR ALTER PROCEDURE dbo.GetAllArtworkImageRelativePaths AS BEGIN
SET
NOCOUNT ON;

SELECT
    artworkImages.RelativePath
FROM
    dbo.ArtworkImages AS artworkImages;

END
