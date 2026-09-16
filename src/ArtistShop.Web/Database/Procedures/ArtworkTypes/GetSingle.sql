CREATE OR ALTER PROCEDURE dbo.GetArtworkType @Id int AS BEGIN
SET
NOCOUNT ON;

SELECT
    Id,
    Name
FROM
    dbo.ArtworkTypes
WHERE
    Id = @Id;

SELECT
    ArtworkFieldId
FROM
    dbo.ArtworkTypeAndArtworkFieldsJunction
WHERE
    ArtworkTypeId = @Id;

END;
