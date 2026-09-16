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

-- the join drops a type deleted in another tab while the form was open: nothing is lost,
-- because no artwork can have that type any more
INSERT INTO
    dbo.VocabularyAndArtworkTypesJunction (VocabularyId, ArtworkTypeId)
SELECT
    @Id,
    artworkType.Id
FROM
    @ArtworkTypeIds AS artworkTypeIds
    JOIN dbo.ArtworkTypes AS artworkType ON artworkType.Id = artworkTypeIds.Id;

COMMIT TRANSACTION;

SELECT
    @Id AS Id;

END;
