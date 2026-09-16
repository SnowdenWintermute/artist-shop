CREATE OR ALTER PROCEDURE dbo.CountVocabularyUsage @Id int AS BEGIN
SET
NOCOUNT ON;

SELECT
    COUNT(*) AS VocabularyTermCount
FROM
    dbo.VocabularyTerms
WHERE
    VocabularyId = @Id;

SELECT
    ArtworkTypeId,
    COUNT(DISTINCT ArtworkId) AS ArtworkCount
FROM
    dbo.ArtworkAndVocabularyTermsJunction AS junction
WHERE
    junction.VocabularyId = @Id
GROUP BY
    junction.ArtworkTypeId;

END;
