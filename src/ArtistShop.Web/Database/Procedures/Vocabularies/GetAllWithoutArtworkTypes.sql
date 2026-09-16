CREATE OR ALTER PROCEDURE dbo.GetVocabulariesWithoutArtworkTypes AS BEGIN
SET
NOCOUNT ON;

SELECT
    vocabulary.Id,
    vocabulary.Name
FROM
    dbo.Vocabularies AS vocabulary
WHERE
    NOT EXISTS (
        SELECT
            1
        FROM
            dbo.VocabularyAndArtworkTypesJunction AS applies
        WHERE
            applies.VocabularyId = vocabulary.Id
    );

END;
