CREATE OR ALTER PROCEDURE dbo.AddVocabulary @Name nvarchar(100),
@ArtworkTypeIds dbo.IdList READONLY AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

BEGIN TRANSACTION;

INSERT INTO
    dbo.Vocabularies (Name)
VALUES
    (@Name);

DECLARE @Id int = SCOPE_IDENTITY();

-- an unknown type id hits the foreign key and fails with error 547
INSERT INTO
    dbo.VocabularyAndArtworkTypesJunction (VocabularyId, ArtworkTypeId)
SELECT
    @Id,
    artworkTypeIds.Id
FROM
    @ArtworkTypeIds AS artworkTypeIds;

COMMIT TRANSACTION;

SELECT
    @Id AS Id;

END;
