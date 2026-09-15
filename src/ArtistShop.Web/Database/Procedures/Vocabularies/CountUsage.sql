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
    ShopItemTypeId,
    COUNT(DISTINCT ShopItemId) AS ShopItemCount
FROM
    dbo.ShopItemAndVocabularyTermsJunction AS junction
WHERE
    junction.VocabularyId = @Id
GROUP BY
    junction.ShopItemTypeId;

END;
