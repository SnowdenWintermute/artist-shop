CREATE OR ALTER PROCEDURE dbo.AddVocabularyTerm @VocabularyId int,
@Name nvarchar(100) AS BEGIN
SET
NOCOUNT ON;

INSERT INTO
    dbo.VocabularyTerms (VocabularyId, Name)
    -- @QUESTION is INSERTED a keyword that means "Give back a table of the ids"
    OUTPUT INSERTED.Id
VALUES
    (@VocabularyId, @Name);

END;
