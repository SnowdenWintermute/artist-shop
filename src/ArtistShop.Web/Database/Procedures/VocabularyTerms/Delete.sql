CREATE OR ALTER PROCEDURE dbo.DeleteVocabularyTerm @Id int AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

BEGIN TRANSACTION;

DELETE FROM dbo.ArtworkAndVocabularyTermsJunction
WHERE
    TermId = @Id;

DELETE FROM dbo.VocabularyTerms
WHERE
    Id = @Id;

COMMIT TRANSACTION;

END;
