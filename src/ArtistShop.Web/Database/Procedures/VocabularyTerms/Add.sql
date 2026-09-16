CREATE OR ALTER PROCEDURE dbo.AddVocabularyTerm @VocabularyId int,
@Name nvarchar(100) AS BEGIN
SET
NOCOUNT ON;

INSERT INTO
    dbo.VocabularyTerms (VocabularyId, Name) OUTPUT INSERTED.Id
VALUES
    (@VocabularyId, @Name);

END;
