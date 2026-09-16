-- the products island's rows
CREATE
TYPE dbo.ProductList AS
TABLE (
    ProductTypeId int NOT NULL,
    Label nvarchar(100),
    Price decimal(10, 2),
    EditionSize int,
    Stock int NOT NULL
);
