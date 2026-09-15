CREATE OR ALTER PROCEDURE dbo.GetVocabulariesWithTermsForShopItemType @ShopItemTypeId int AS BEGIN
SET
NOCOUNT ON;

-- one row per term; a vocabulary with no terms still gets one row, with NULL term columns
SELECT
    vocabulary.Id,
    vocabulary.Name,
    term.Id AS TermId,
    term.Name AS TermName
FROM
    dbo.Vocabularies AS vocabulary
    JOIN dbo.VocabularyAndShopItemTypesJunction AS applies ON applies.VocabularyId = vocabulary.Id
    LEFT JOIN dbo.VocabularyTerms AS term ON term.VocabularyId = vocabulary.Id
WHERE
    applies.ShopItemTypeId = @ShopItemTypeId;

END;
