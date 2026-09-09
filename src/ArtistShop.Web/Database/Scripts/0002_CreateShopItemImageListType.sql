CREATE
TYPE dbo.ShopItemImageList AS
TABLE (
    Path nvarchar(400) NOT NULL,
    SortOrder int NOT NULL,
    IsPrimary bit NOT NULL
);
