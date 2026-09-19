CREATE OR ALTER PROCEDURE dbo.GetProductTypes AS BEGIN
SET
NOCOUNT ON;

SELECT
    productType.Id,
    productType.Name,
    productType.IsDefault
FROM
    dbo.ProductTypes AS productType;

END;
