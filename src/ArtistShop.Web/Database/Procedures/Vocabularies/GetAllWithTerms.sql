CREATE OR ALTER PROCEDURE dbo.GetVocabulariesWithTerms AS BEGIN
SET
NOCOUNT ON;

-- every vocabulary, whichever work types it applies to: the artwork list filters across
-- all of them at once. One row per term, and a vocabulary with no terms still gets one
SELECT
    vocabulary.Id,
    vocabulary.Name,
    term.Id AS TermId,
    term.Name AS TermName
FROM
    dbo.Vocabularies AS vocabulary
    LEFT JOIN dbo.VocabularyTerms AS term ON term.VocabularyId = vocabulary.Id;

END;
