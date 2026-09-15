CREATE OR ALTER PROCEDURE dbo.AddVocabulary @Name nvarchar(100),
@ShopItemTypeIds dbo.IdList READONLY AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

BEGIN TRANSACTION;

INSERT INTO
    dbo.Vocabularies (Name)
VALUES
    (@Name);

DECLARE @Id int = SCOPE_IDENTITY();

-- an unknown type id hits the foreign key and fails with error 547
INSERT INTO
    dbo.VocabularyAndShopItemTypesJunction (VocabularyId, ShopItemTypeId)
SELECT
    @Id,
    shopItemTypeIds.Id
FROM
    @ShopItemTypeIds AS shopItemTypeIds;

COMMIT TRANSACTION;

SELECT
    @Id AS Id;

END;
