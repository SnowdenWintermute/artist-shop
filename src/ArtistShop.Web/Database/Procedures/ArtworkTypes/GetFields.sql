CREATE OR ALTER PROCEDURE dbo.GetArtworkTypeFields @ArtworkTypeId int AS BEGIN
SET
NOCOUNT ON;

SELECT
    ArtworkFieldId
FROM
    dbo.ArtworkTypeAndArtworkFieldsJunction
WHERE
    ArtworkTypeId = @ArtworkTypeId;

END;
