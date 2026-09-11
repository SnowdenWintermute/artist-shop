CREATE TABLE dbo.ShopItems (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_ShopItems PRIMARY KEY (Id),
    Name nvarchar(200) NOT NULL,
    Slug nvarchar(200) NOT NULL,
    CONSTRAINT Unique_ShopItems_Slug UNIQUE (Slug),
    Price decimal(10, 2) NOT NULL,
    CONSTRAINT Check_ShopItems_Price CHECK (Price >= 0),
    Stock int NOT NULL,
    CONSTRAINT Check_ShopItems_Stock CHECK (Stock >= 0),
    CreatedAt datetime2 NOT NULL CONSTRAINT DF_ShopItems_CreatedAt DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Paintings (
    Id int,
    CONSTRAINT PrimaryKey_Paintings PRIMARY KEY (Id),
    CONSTRAINT ForeignKey_Paintings_ShopItems FOREIGN KEY (Id) REFERENCES dbo.ShopItems (Id) ON DELETE CASCADE,
    DatePainted date NOT NULL,
    Description nvarchar(max),
    WidthCm decimal(6, 2),
    HeightCm decimal(6, 2),
    CONSTRAINT Check_Paintings_Dimensions CHECK (
        (
            WidthCm IS NULL
            AND HeightCm IS NULL
        )
        OR (
            WidthCm IS NOT NULL
            AND HeightCm IS NOT NULL
        )
    ),
    -- will pass if width/height null because x > 0 when x is null is UNKNOWN,
    -- and constraint only fail if evaluate to false
    CONSTRAINT Check_Paintings_WidthCm CHECK (WidthCm > 0),
    CONSTRAINT Check_Paintings_HeightCm CHECK (HeightCm > 0)
);

CREATE TABLE dbo.ShopItemImages (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_ShopItemImages PRIMARY KEY (Id),
    ShopItemId int NOT NULL,
    CONSTRAINT ForeignKey_ShopItemImages_ShopItems FOREIGN KEY (ShopItemId) REFERENCES dbo.ShopItems (Id) ON DELETE CASCADE,
    RelativePath nvarchar(400) NOT NULL,
    SortOrder int NOT NULL,
    -- DEFAULT can't be put in a standalone constraint
    IsPrimary bit NOT NULL CONSTRAINT Default_ShopItemImages_IsPrimary DEFAULT 0,
    Width int NOT NULL,
    Height int NOT NULL
);

CREATE UNIQUE INDEX UniqueIndex_ShopItemImages_Primary ON dbo.ShopItemImages (ShopItemId)
WHERE
    IsPrimary = 1;

CREATE TABLE dbo.Mediums (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_Mediums PRIMARY KEY (Id),
    Name nvarchar(100) NOT NULL,
    -- UNIQUE rejects duplicates but does not define what a duplicate is -- the column's
    -- collation does. The default here is SQL_Latin1_General_CP1_CI_AS: CI = case-insensitive,
    -- AS = accent-sensitive. So 'Oil' collides with 'oil', but 'cafe' and 'cafe' with an
    -- accent do not. Postgres compares bytes and would allow both spellings of Oil.
    CONSTRAINT Unique_Mediums_Name UNIQUE (Name)
);

CREATE TABLE dbo.Supports (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_Supports PRIMARY KEY (Id),
    Name nvarchar(100) NOT NULL,
    CONSTRAINT Unique_Supports_Name UNIQUE (Name)
);

CREATE TABLE dbo.PaintingSeries (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_PaintingSeries PRIMARY KEY (Id),
    Name nvarchar(256) NOT NULL,
    CONSTRAINT Unique_PaintingSeries_Name UNIQUE (Name),
    Slug nvarchar(256) NOT NULL,
    CONSTRAINT Unique_PaintingSeries_Slug UNIQUE (Slug)
);

CREATE TABLE dbo.PaintingAndMediumsJunction (
    PaintingId int NOT NULL,
    MediumId int NOT NULL,
    CONSTRAINT PrimaryKey_PaintingAndMediumsJunction PRIMARY KEY (PaintingId, MediumId),
    CONSTRAINT ForeignKey_PaintingAndMediumsJunction_Paintings FOREIGN KEY (PaintingId) REFERENCES dbo.Paintings (Id) ON DELETE CASCADE,
    CONSTRAINT ForeignKey_PaintingAndMediumsJunction_Mediums FOREIGN KEY (MediumId) REFERENCES dbo.Mediums (Id)
);

CREATE TABLE dbo.PaintingAndSupportsJunction (
    PaintingId int NOT NULL,
    SupportId int NOT NULL,
    CONSTRAINT PrimaryKey_PaintingAndSupportsJunction PRIMARY KEY (PaintingId, SupportId),
    CONSTRAINT ForeignKey_PaintingAndSupportsJunction_Paintings FOREIGN KEY (PaintingId) REFERENCES dbo.Paintings (Id) ON DELETE CASCADE,
    CONSTRAINT ForeignKey_PaintingAndSupportsJunction_Supports FOREIGN KEY (SupportId) REFERENCES dbo.Supports (Id)
);

CREATE TABLE dbo.PaintingAndSeriesJunction (
    PaintingId int NOT NULL,
    SeriesId int NOT NULL,
    CONSTRAINT PrimaryKey_PaintingAndSeriesJunction PRIMARY KEY (PaintingId, SeriesId),
    CONSTRAINT ForeignKey_PaintingAndSeriesJunction_Paintings FOREIGN KEY (PaintingId) REFERENCES dbo.Paintings (Id) ON DELETE CASCADE,
    CONSTRAINT ForeignKey_PaintingAndSeriesJunction_PaintingSeries FOREIGN KEY (SeriesId) REFERENCES dbo.PaintingSeries (Id)
);
