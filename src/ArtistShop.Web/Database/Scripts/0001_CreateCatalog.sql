CREATE TABLE dbo.ShopItemTypes (
    Id int,
    CONSTRAINT PrimaryKey_ShopItemTypes PRIMARY KEY (Id),
    Name nvarchar(50) NOT NULL,
    CONSTRAINT Unique_ShopItemTypes_Name UNIQUE (Name)
);

INSERT INTO
    dbo.ShopItemTypes (Id, Name)
VALUES
    -- N turns the string into unicode
    (1, N'Painting');

CREATE TABLE dbo.ShopItems (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_ShopItems PRIMARY KEY (Id),
    ShopItemTypeId int NOT NULL,
    CONSTRAINT ForeignKey_ShopItems_ShopItemTypes FOREIGN KEY (ShopItemTypeId) REFERENCES dbo.ShopItemTypes (Id),
    CONSTRAINT Unique_ShopItems_IdShopItemType UNIQUE (Id, ShopItemTypeId),
    Name nvarchar(200) NOT NULL,
    Slug nvarchar(200) NOT NULL,
    CONSTRAINT Unique_ShopItems_Slug UNIQUE (Slug),
    Price decimal(10, 2),
    CONSTRAINT Check_ShopItems_Price CHECK (Price >= 0),
    Stock int NOT NULL,
    CONSTRAINT Check_ShopItems_Stock CHECK (Stock >= 0),
    CreatedAt datetime2 NOT NULL CONSTRAINT DF_ShopItems_CreatedAt DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Vocabularies (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_Vocabularies PRIMARY KEY (Id),
    Name nvarchar(100) NOT NULL,
    CONSTRAINT Unique_Vocabularies_Name UNIQUE (Name)
);

-- sets which vocabulary types are allowed on which shop item types
CREATE TABLE dbo.VocabularyAndShopItemTypesJunction (
    VocabularyId int NOT NULL,
    ShopItemTypeId int NOT NULL,
    CONSTRAINT PrimaryKey_VocabularyAndShopItemTypesJunction PRIMARY KEY (VocabularyId, ShopItemTypeId),
    CONSTRAINT ForeignKey_VocabularyAndShopItemTypesJunction_Vocabularies FOREIGN KEY (VocabularyId) REFERENCES dbo.Vocabularies (Id),
    CONSTRAINT ForeignKey_VocabularyAndShopItemTypesJunction_ShopItemTypes FOREIGN KEY (ShopItemTypeId) REFERENCES dbo.ShopItemTypes (Id)
);

-- The enumerated words of a certain vocabulary, like if the vocabulary is "Support"
-- the terms could be "Paper", "Canvas" etc
CREATE TABLE dbo.VocabularyTerms (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_VocabularyTerms PRIMARY KEY (Id),
    VocabularyId int NOT NULL,
    CONSTRAINT ForeignKey_VocabularyTerms_Vocabularies FOREIGN KEY (VocabularyId) REFERENCES dbo.Vocabularies (Id),
    Name nvarchar(100) NOT NULL,
    -- "Paper" can be both a "Support" and a "Medium", but not a support twice
    -- UNIQUE rejects duplicates but does not define what a duplicate is -- the column's
    -- collation does. The default here is SQL_Latin1_General_CP1_CI_AS: CI = case-insensitive,
    -- AS = accent-sensitive. So 'Oil' collides with 'oil', but 'cafe' and 'cafe' with an
    -- accent do not. Postgres compares bytes and would allow both spellings of Oil.
    CONSTRAINT Unique_VocabularyTerms_VocabularyName UNIQUE (VocabularyId, Name),
    CONSTRAINT Unique_VocabularyTerms_IdVocabulary UNIQUE (Id, VocabularyId)
);

CREATE TABLE dbo.ShopItemAndVocabularyTermsJunction (
    ShopItemId int NOT NULL,
    ShopItemTypeId int NOT NULL,
    TermId int NOT NULL,
    VocabularyId int NOT NULL,
    CONSTRAINT PrimaryKey_ShopItemAndVocabularyTermsJunction PRIMARY KEY (ShopItemId, TermId),
    CONSTRAINT ForeignKey_ShopItemAndVocabularyTermsJunction_ShopItems FOREIGN KEY (ShopItemId, ShopItemTypeId) REFERENCES dbo.ShopItems (Id, ShopItemTypeId) ON DELETE CASCADE,
    CONSTRAINT ForeignKey_ShopItemAndVocabularyTermsJunction_VocabularyTerms FOREIGN KEY (TermId, VocabularyId) REFERENCES dbo.VocabularyTerms (Id, VocabularyId),
    CONSTRAINT ForeignKey_ShopItemAndVocabularyTermsJunction_VocabularyAndShopItemTypesJunction FOREIGN KEY (VocabularyId, ShopItemTypeId) REFERENCES dbo.VocabularyAndShopItemTypesJunction (VocabularyId, ShopItemTypeId)
);

CREATE TABLE dbo.Paintings (
    Id int,
    CONSTRAINT PrimaryKey_Paintings PRIMARY KEY (Id),
    -- a computed column: its value is this expression, not something inserted. PERSISTED
    -- stores it in the row, which a computed column needs before a foreign key can use it.
    ShopItemTypeId AS 1 PERSISTED NOT NULL,
    -- this row's shop item must be marked as a painting
    CONSTRAINT ForeignKey_Paintings_ShopItems FOREIGN KEY (Id, ShopItemTypeId) REFERENCES dbo.ShopItems (Id, ShopItemTypeId) ON DELETE CASCADE,
    DatePainted date,
    DatePaintedPrecision tinyint,
    CONSTRAINT Check_Paintings_DatePainted CHECK (
        (
            DatePainted IS NULL
            AND DatePaintedPrecision IS NULL
        )
        OR (
            DatePainted IS NOT NULL
            AND DatePaintedPrecision IS NOT NULL
        )
    ),
    CONSTRAINT Check_Paintings_DatePaintedPrecision CHECK (DatePaintedPrecision IN (1, 2, 3)),
    -- the parts below the precision must be "the first": a year-only date is stored
    -- as January 1st, so two paintings from "2019" can't hold different hidden days
    CONSTRAINT Check_Paintings_DatePaintedUnknownParts CHECK (
        DatePaintedPrecision = 3
        OR (
            DatePaintedPrecision = 2
            AND DAY(DatePainted) = 1
        )
        OR (
            DatePaintedPrecision = 1
            AND MONTH(DatePainted) = 1
            AND DAY(DatePainted) = 1
        )
    ),
    Description nvarchar(max),
    -- 4 digits before the decimal point and 4 after, so an inch value with
    -- 2 decimals converts to centimetres with no rounding
    WidthCm decimal(8, 4),
    HeightCm decimal(8, 4),
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
    CONSTRAINT Unique_ShopItemImages_RelativePath UNIQUE (RelativePath),
    OriginalFileName nvarchar(260),
    SortOrder int NOT NULL,
    -- DEFAULT can't be put in a standalone constraint
    IsPrimary bit NOT NULL CONSTRAINT Default_ShopItemImages_IsPrimary DEFAULT 0,
    Width int NOT NULL,
    Height int NOT NULL,
    BlurDataUri nvarchar(1000)
);

CREATE UNIQUE INDEX UniqueIndex_ShopItemImages_Primary ON dbo.ShopItemImages (ShopItemId)
WHERE
    IsPrimary = 1;

CREATE TABLE dbo.PaintingSeries (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_PaintingSeries PRIMARY KEY (Id),
    Name nvarchar(256) NOT NULL,
    CONSTRAINT Unique_PaintingSeries_Name UNIQUE (Name),
    Slug nvarchar(256) NOT NULL,
    CONSTRAINT Unique_PaintingSeries_Slug UNIQUE (Slug)
);

CREATE TABLE dbo.PaintingAndSeriesJunction (
    PaintingId int NOT NULL,
    SeriesId int NOT NULL,
    SortOrder int NOT NULL,
    CONSTRAINT Unique_PaintingAndSeriesJunction_SeriesSortOrder UNIQUE (SeriesId, SortOrder),
    CONSTRAINT PrimaryKey_PaintingAndSeriesJunction PRIMARY KEY (PaintingId, SeriesId),
    CONSTRAINT ForeignKey_PaintingAndSeriesJunction_Paintings FOREIGN KEY (PaintingId) REFERENCES dbo.Paintings (Id) ON DELETE CASCADE,
    CONSTRAINT ForeignKey_PaintingAndSeriesJunction_PaintingSeries FOREIGN KEY (SeriesId) REFERENCES dbo.PaintingSeries (Id)
);
