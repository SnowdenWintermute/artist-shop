CREATE OR ALTER PROCEDURE dbo.GetAllArtworkImageStorageKeys AS BEGIN
SET
NOCOUNT ON;

SELECT
    artworkImages.StorageKey
FROM
    dbo.ArtworkImages AS artworkImages;

END
