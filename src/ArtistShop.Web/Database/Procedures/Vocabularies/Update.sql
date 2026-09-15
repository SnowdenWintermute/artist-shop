CREATE OR ALTER PROCEDURE dbo.UpdateVocabulary @Id int,
@Name nvarchar(100),
@ShopItemTypeIds dbo.IdList READONLY AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

BEGIN TRANSACTION;

UPDATE dbo.Vocabularies
SET
    Name = @Name
WHERE
    Id = @Id;

IF @@ROWCOUNT = 0 THROW 50002,
'The vocabulary no longer exists.',
1;

-- VocabularyTerms must be removed from ShopItems first; the foreign key refuses removing a vocabularyShopItemAssociation that's still in use
-- @QUESTION can we make this a stored procedure and call it from here?
DELETE vocabularyTermShopItemAssociation
FROM
    dbo.ShopItemAndVocabularyTermsJunction AS vocabularyTermShopItemAssociation
WHERE
    vocabularyTermShopItemAssociation.VocabularyId = @Id
    AND NOT EXISTS (
        -- @QUESTION can this be a CTE
        SELECT
            1
        FROM
            @ShopItemTypeIds AS shopItemTypeIds
        WHERE
            shopItemTypeIds.Id = vocabularyTermShopItemAssociation.ShopItemTypeId
    );

DELETE vocabularyShopItemAssociation
FROM
    dbo.VocabularyAndShopItemTypesJunction AS vocabularyShopItemAssociation
WHERE
    vocabularyShopItemAssociation.VocabularyId = @Id
    AND NOT EXISTS (
        SELECT
            1
        FROM
            @ShopItemTypeIds AS shopItemTypeIds
        WHERE
            shopItemTypeIds.Id = vocabularyShopItemAssociation.ShopItemTypeId
    );

INSERT INTO
    dbo.VocabularyAndShopItemTypesJunction (VocabularyId, ShopItemTypeId)
SELECT
    @Id,
    shopItemTypeIds.Id
FROM
    @ShopItemTypeIds AS shopItemTypeIds
WHERE
    NOT EXISTS (
        SELECT
            1
        FROM
            dbo.VocabularyAndShopItemTypesJunction AS existing
        WHERE
            existing.VocabularyId = @Id
            AND existing.ShopItemTypeId = shopItemTypeIds.Id
    );

COMMIT TRANSACTION;

END;
