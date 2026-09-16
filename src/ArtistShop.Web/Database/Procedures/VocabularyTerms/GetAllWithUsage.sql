CREATE OR ALTER PROCEDURE dbo.GetVocabularyTermsWithUsage @VocabularyId int AS BEGIN
SET
NOCOUNT ON;

SELECT
    vocabularyTerm.Id,
    vocabularyTerm.Name,
    -- not COUNT(*) because that would count null entries
    COUNT(association.ArtworkId) AS ArtworkCount
FROM
    dbo.VocabularyTerms AS vocabularyTerm
    -- We want to include terms with no associations
    LEFT JOIN dbo.ArtworkAndVocabularyTermsJunction AS association ON association.TermId = vocabularyTerm.Id
WHERE
    vocabularyTerm.VocabularyId = @VocabularyId
GROUP BY
    vocabularyTerm.Id,
    vocabularyTerm.Name;

END;
