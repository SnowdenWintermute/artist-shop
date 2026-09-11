CREATE
TYPE dbo.ShopItemImageList AS
TABLE (
    RelativePath nvarchar(400) NOT NULL,
    SortOrder int NOT NULL,
    IsPrimary bit NOT NULL,
    Width int NOT NULL,
    Height int NOT NULL,
    BlurDataUri nvarchar(1000)
);

CREATE
TYPE dbo.IdList AS
TABLE (Id int NOT NULL);
