CREATE TABLE dbo.ShopItems (
    Id int IDENTITY(1, 1) CONSTRAINT PrimaryKey_ShopItems PRIMARY KEY (Id),
    Name nvarchar(200) NOT NULL,
    Slug nvarchar(200) NOT NULL CONSTRAINT Unique_ShopItems_Slug UNIQUE (Slug),
    Price decimal(10, 2) NOT NULL CONSTRAINT Check_ShopItems_Price CHECK (Price >= 0),
    Stock int NOT NULL CONSTRAINT Check_ShopItems_Stock CHECK (Stock >= 0),
    CreatedAt datetime2 NOT NULL CONSTRAINT DF_ShopItems_CreatedAt DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Paintings (
    ShopItemId int CONSTRAINT PrivateKey_Paintings PRIMARY KEY (ShopItemId),
    CONSTRAINT ForeignKey_Paintings_ShopItems FOREIGN KEY (ShopItemId) REFERENCES dbo.ShopItems (Id) ON DELETE CASCADE,
    DatePainted date NOT NULL,
    Description nvarchar(max) NULL, -- why write null here?
    WidthCm decimal(6, 2) NULL,
    HeightCm decimal(6, 2) NULL,
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
)
