CREATE OR ALTER PROCEDURE dbo.AddArtworkType @Name nvarchar(50),
@ArtworkFieldIds dbo.IdList READONLY AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

BEGIN TRANSACTION;

INSERT INTO
    dbo.ArtworkTypes (Name)
VALUES
    (@Name);

DECLARE @Id int = SCOPE_IDENTITY();

-- a field whose required field isn't in the list fails the junction's self-referencing foreign key (547)
INSERT INTO
    dbo.ArtworkTypeAndArtworkFieldsJunction (ArtworkTypeId, ArtworkFieldId, RequiresArtworkFieldId)
SELECT
    @Id,
    artworkField.Id,
    artworkField.RequiresArtworkFieldId
FROM
    @ArtworkFieldIds AS artworkFieldIds
    JOIN dbo.ArtworkFields AS artworkField ON artworkField.Id = artworkFieldIds.Id;

COMMIT TRANSACTION;

SELECT
    @Id AS Id;

END;
