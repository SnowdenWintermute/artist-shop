CREATE OR ALTER PROCEDURE dbo.GetArtworkFields AS BEGIN
SET
NOCOUNT ON;

SELECT
    Id,
    Name,
    RequiresArtworkFieldId
FROM
    dbo.ArtworkFields;

END;
