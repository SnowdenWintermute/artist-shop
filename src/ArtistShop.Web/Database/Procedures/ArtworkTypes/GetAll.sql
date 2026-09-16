CREATE OR ALTER PROCEDURE dbo.GetArtworkTypes AS BEGIN
SET
NOCOUNT ON;

SELECT
    artworkType.Id,
    artworkType.Name
FROM
    dbo.ArtworkTypes AS artworkType;

END;
