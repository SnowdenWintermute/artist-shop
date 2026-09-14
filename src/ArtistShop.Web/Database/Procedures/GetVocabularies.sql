CREATE OR ALTER PROCEDURE dbo.GetVocabularies AS BEGIN
SET
NOCOUNT ON;

SELECT
    vocabulary.Id,
    vocabulary.Name
FROM
    dbo.Vocabularies AS vocabulary;

END;
