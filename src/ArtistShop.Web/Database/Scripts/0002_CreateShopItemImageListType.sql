CREATE
TYPE dbo.ShopItemImageList AS
TABLE (
    Path nvarchar(400) NOT NULL,
    SortOrder int NOT NULL,
    IsPrimary bit NOT NULL
);

CREATE
TYPE dbo.IdList AS
TABLE (Id int NOT NULL);
