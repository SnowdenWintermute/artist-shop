CREATE OR ALTER PROCEDURE dbo.GetProductKinds AS BEGIN
SET
NOCOUNT ON;

SELECT
    productKind.Id,
    productKind.Name
FROM
    dbo.ProductKinds AS productKind;

END;
