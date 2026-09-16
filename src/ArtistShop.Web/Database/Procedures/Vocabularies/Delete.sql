CREATE OR ALTER PROCEDURE dbo.DeleteVocabulary @Id int AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

BEGIN TRANSACTION;

DELETE FROM dbo.ArtworkAndVocabularyTermsJunction
WHERE
    VocabularyId = @Id;

DELETE FROM dbo.VocabularyTerms
WHERE
    VocabularyId = @Id;

DELETE FROM dbo.VocabularyAndArtworkTypesJunction
WHERE
    VocabularyId = @Id;

DELETE FROM dbo.Vocabularies
WHERE
    Id = @Id;

COMMIT TRANSACTION;

END;
