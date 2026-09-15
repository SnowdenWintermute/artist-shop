CREATE OR ALTER PROCEDURE dbo.GetVocabulariesWithoutShopItemTypes AS BEGIN
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
            dbo.VocabularyAndShopItemTypesJunction AS applies
        WHERE
            applies.VocabularyId = vocabulary.Id
    );

END;
