CREATE OR ALTER PROCEDURE dbo.GetVocabulariesWithTerms @ArtworkTypeId int = NULL AS BEGIN
SET
NOCOUNT ON;

-- one row per term; a vocabulary with no terms still gets one row, with NULL term columns.
-- No artwork type means every vocabulary, which is what the artwork list filters across
SELECT
    vocabulary.Id,
    vocabulary.Name,
    term.Id AS TermId,
    term.Name AS TermName
FROM
    dbo.Vocabularies AS vocabulary
    LEFT JOIN dbo.VocabularyTerms AS term ON term.VocabularyId = vocabulary.Id
WHERE
    @ArtworkTypeId IS NULL
    OR EXISTS (
        SELECT
            1
        FROM
            dbo.VocabularyAndArtworkTypesJunction AS applies
        WHERE
            applies.VocabularyId = vocabulary.Id
            AND applies.ArtworkTypeId = @ArtworkTypeId
    );

END;
