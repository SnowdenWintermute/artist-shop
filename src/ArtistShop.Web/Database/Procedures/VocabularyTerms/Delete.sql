CREATE OR ALTER PROCEDURE dbo.DeleteVocabularyTerm @Id int AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

BEGIN TRANSACTION;

-- @QUESTION can we use this procedure in delete vocabulary?
DELETE FROM dbo.ShopItemAndVocabularyTermsJunction
WHERE
    TermId = @Id;

DELETE FROM dbo.VocabularyTerms
WHERE
    Id = @Id;

COMMIT TRANSACTION;

END;
