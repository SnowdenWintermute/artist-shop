CREATE TABLE dbo.ArtworkTypes (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_ArtworkTypes PRIMARY KEY (Id),
    Name nvarchar(50) NOT NULL,
    CONSTRAINT Unique_ArtworkTypes_Name UNIQUE (Name)
);

INSERT INTO
    dbo.ArtworkTypes (Name)
VALUES
    -- N turns the string into unicode
    (N'Painting'),
    (N'Photograph'),
    (N'Sculpture');

-- defined by us; the ids must match the ArtworkField enum in C#
CREATE TABLE dbo.ArtworkFields (
    Id int,
    CONSTRAINT PrimaryKey_ArtworkFields PRIMARY KEY (Id),
    Name nvarchar(50) NOT NULL,
    CONSTRAINT Unique_ArtworkFields_Name UNIQUE (Name),
    -- a field that only makes sense alongside another, like depth alongside height and width. A field
    -- with no requirement names itself: NOT NULL matters, because a foreign key with a NULL column
    -- isn't checked at all, which would let the junction below skip the requirement
    RequiresArtworkFieldId int NOT NULL,
    CONSTRAINT ForeignKey_ArtworkFields_RequiresArtworkField FOREIGN KEY (RequiresArtworkFieldId) REFERENCES dbo.ArtworkFields (Id),
    -- lets the junction below copy the requirement under a foreign key
    CONSTRAINT Unique_ArtworkFields_IdRequires UNIQUE (Id, RequiresArtworkFieldId)
);

INSERT INTO
    dbo.ArtworkFields (Id, Name, RequiresArtworkFieldId)
VALUES
    (1, N'Date created', 1),
    (2, N'Height and width', 2),
    (3, N'Depth', 2),
    (4, N'Duration', 4);

-- which fields the artist switched on for each type
CREATE TABLE dbo.ArtworkTypeAndArtworkFieldsJunction (
    ArtworkTypeId int NOT NULL,
    ArtworkFieldId int NOT NULL,
    -- copied from ArtworkFields; the foreign key to ArtworkFields keeps the copy honest
    RequiresArtworkFieldId int NOT NULL,
    CONSTRAINT PrimaryKey_ArtworkTypeAndArtworkFieldsJunction PRIMARY KEY (ArtworkTypeId, ArtworkFieldId),
    CONSTRAINT ForeignKey_ArtworkTypeAndArtworkFieldsJunction_ArtworkTypes FOREIGN KEY (ArtworkTypeId) REFERENCES dbo.ArtworkTypes (Id),
    CONSTRAINT ForeignKey_ArtworkTypeAndArtworkFieldsJunction_ArtworkFields FOREIGN KEY (ArtworkFieldId, RequiresArtworkFieldId) REFERENCES dbo.ArtworkFields (Id, RequiresArtworkFieldId),
    -- points at a row of this same table: the type must also have the required field. A field that
    -- requires itself points at its own row, so it always passes
    CONSTRAINT ForeignKey_ArtworkTypeAndArtworkFieldsJunction_RequiredField FOREIGN KEY (ArtworkTypeId, RequiresArtworkFieldId) REFERENCES dbo.ArtworkTypeAndArtworkFieldsJunction (ArtworkTypeId, ArtworkFieldId)
);

-- a table value constructor: VALUES used as a table of rows, named like any other table
INSERT INTO
    dbo.ArtworkTypeAndArtworkFieldsJunction (ArtworkTypeId, ArtworkFieldId, RequiresArtworkFieldId)
SELECT
    artworkType.Id,
    artworkField.Id,
    artworkField.RequiresArtworkFieldId
FROM
    (
        VALUES
            (N'Painting', 1),
            (N'Painting', 2),
            (N'Photograph', 1),
            (N'Photograph', 2),
            (N'Sculpture', 1),
            (N'Sculpture', 2),
            (N'Sculpture', 3)
    ) AS seed (ArtworkTypeName, ArtworkFieldId)
    JOIN dbo.ArtworkTypes AS artworkType ON artworkType.Name = seed.ArtworkTypeName
    JOIN dbo.ArtworkFields AS artworkField ON artworkField.Id = seed.ArtworkFieldId;

CREATE TABLE dbo.Artworks (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_Artworks PRIMARY KEY (Id),
    ArtworkTypeId int NOT NULL,
    CONSTRAINT ForeignKey_Artworks_ArtworkTypes FOREIGN KEY (ArtworkTypeId) REFERENCES dbo.ArtworkTypes (Id),
    CONSTRAINT Unique_Artworks_IdArtworkType UNIQUE (Id, ArtworkTypeId),
    Name nvarchar(200) NOT NULL,
    Slug nvarchar(200) NOT NULL,
    CONSTRAINT Unique_Artworks_Slug UNIQUE (Slug),
    Description nvarchar(max),
    DateCreated date,
    DateCreatedPrecision tinyint,
    CONSTRAINT Check_Artworks_DateCreated CHECK (
        (
            DateCreated IS NULL
            AND DateCreatedPrecision IS NULL
        )
        OR (
            DateCreated IS NOT NULL
            AND DateCreatedPrecision IS NOT NULL
        )
    ),
    CONSTRAINT Check_Artworks_DateCreatedPrecision CHECK (DateCreatedPrecision IN (1, 2, 3)),
    -- the parts below the precision must be "the first": a year-only date is stored
    -- as January 1st, so two artworks from "2019" can't hold different hidden days
    CONSTRAINT Check_Artworks_DateCreatedUnknownParts CHECK (
        DateCreatedPrecision = 3
        OR (
            DateCreatedPrecision = 2
            AND DAY(DateCreated) = 1
        )
        OR (
            DateCreatedPrecision = 1
            AND MONTH(DateCreated) = 1
            AND DAY(DateCreated) = 1
        )
    ),
    -- 4 digits before the decimal point and 4 after, so an inch value with
    -- 2 decimals converts to centimetres with no rounding
    -- in the order galleries list them: height x width x depth
    HeightCm decimal(8, 4),
    WidthCm decimal(8, 4),
    DepthCm decimal(8, 4),
    CONSTRAINT Check_Artworks_HeightAndWidth CHECK (
        (
            HeightCm IS NULL
            AND WidthCm IS NULL
        )
        OR (
            HeightCm IS NOT NULL
            AND WidthCm IS NOT NULL
        )
    ),
    CONSTRAINT Check_Artworks_DepthNeedsHeightAndWidth CHECK (
        DepthCm IS NULL
        OR HeightCm IS NOT NULL
    ),
    -- will pass if height/width null because x > 0 when x is null is UNKNOWN,
    -- and constraint only fail if evaluate to false
    CONSTRAINT Check_Artworks_HeightCm CHECK (HeightCm > 0),
    CONSTRAINT Check_Artworks_WidthCm CHECK (WidthCm > 0),
    CONSTRAINT Check_Artworks_DepthCm CHECK (DepthCm > 0),
    DurationSeconds int,
    CONSTRAINT Check_Artworks_DurationSeconds CHECK (DurationSeconds > 0),
    CreatedAt datetime2 NOT NULL CONSTRAINT Default_Artworks_CreatedAt DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Vocabularies (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_Vocabularies PRIMARY KEY (Id),
    Name nvarchar(100) NOT NULL,
    CONSTRAINT Unique_Vocabularies_Name UNIQUE (Name)
);

-- sets which vocabularies are allowed on which artwork types
CREATE TABLE dbo.VocabularyAndArtworkTypesJunction (
    VocabularyId int NOT NULL,
    ArtworkTypeId int NOT NULL,
    CONSTRAINT PrimaryKey_VocabularyAndArtworkTypesJunction PRIMARY KEY (VocabularyId, ArtworkTypeId),
    CONSTRAINT ForeignKey_VocabularyAndArtworkTypesJunction_Vocabularies FOREIGN KEY (VocabularyId) REFERENCES dbo.Vocabularies (Id),
    CONSTRAINT ForeignKey_VocabularyAndArtworkTypesJunction_ArtworkTypes FOREIGN KEY (ArtworkTypeId) REFERENCES dbo.ArtworkTypes (Id)
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

CREATE TABLE dbo.ArtworkAndVocabularyTermsJunction (
    ArtworkId int NOT NULL,
    ArtworkTypeId int NOT NULL,
    TermId int NOT NULL,
    VocabularyId int NOT NULL,
    CONSTRAINT PrimaryKey_ArtworkAndVocabularyTermsJunction PRIMARY KEY (ArtworkId, TermId),
    CONSTRAINT ForeignKey_ArtworkAndVocabularyTermsJunction_Artworks FOREIGN KEY (ArtworkId, ArtworkTypeId) REFERENCES dbo.Artworks (Id, ArtworkTypeId) ON DELETE CASCADE,
    CONSTRAINT ForeignKey_ArtworkAndVocabularyTermsJunction_VocabularyTerms FOREIGN KEY (TermId, VocabularyId) REFERENCES dbo.VocabularyTerms (Id, VocabularyId),
    CONSTRAINT ForeignKey_ArtworkAndVocabularyTermsJunction_VocabularyAndArtworkTypesJunction FOREIGN KEY (VocabularyId, ArtworkTypeId) REFERENCES dbo.VocabularyAndArtworkTypesJunction (VocabularyId, ArtworkTypeId)
);

CREATE TABLE dbo.ArtworkImages (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_ArtworkImages PRIMARY KEY (Id),
    ArtworkId int NOT NULL,
    CONSTRAINT ForeignKey_ArtworkImages_Artworks FOREIGN KEY (ArtworkId) REFERENCES dbo.Artworks (Id) ON DELETE CASCADE,
    RelativePath nvarchar(400) NOT NULL,
    CONSTRAINT Unique_ArtworkImages_RelativePath UNIQUE (RelativePath),
    OriginalFileName nvarchar(260),
    SortOrder int NOT NULL,
    -- DEFAULT can't be put in a standalone constraint
    IsPrimary bit NOT NULL CONSTRAINT Default_ArtworkImages_IsPrimary DEFAULT 0,
    Width int NOT NULL,
    Height int NOT NULL,
    BlurDataUri nvarchar(1000)
);

CREATE UNIQUE INDEX UniqueIndex_ArtworkImages_Primary ON dbo.ArtworkImages (ArtworkId)
WHERE
    IsPrimary = 1;

CREATE TABLE dbo.Series (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_Series PRIMARY KEY (Id),
    Name nvarchar(256) NOT NULL,
    CONSTRAINT Unique_Series_Name UNIQUE (Name),
    Slug nvarchar(200) NOT NULL,
    CONSTRAINT Unique_Series_Slug UNIQUE (Slug),
    -- the artist's order, the default visitors see
    SortOrder int NOT NULL,
    CONSTRAINT Unique_Series_SortOrder UNIQUE (SortOrder)
);

-- any artwork type can join any series, mixed freely
CREATE TABLE dbo.ArtworkAndSeriesJunction (
    ArtworkId int NOT NULL,
    SeriesId int NOT NULL,
    SortOrder int NOT NULL,
    CONSTRAINT Unique_ArtworkAndSeriesJunction_SeriesSortOrder UNIQUE (SeriesId, SortOrder),
    CONSTRAINT PrimaryKey_ArtworkAndSeriesJunction PRIMARY KEY (ArtworkId, SeriesId),
    CONSTRAINT ForeignKey_ArtworkAndSeriesJunction_Artworks FOREIGN KEY (ArtworkId) REFERENCES dbo.Artworks (Id) ON DELETE CASCADE,
    CONSTRAINT ForeignKey_ArtworkAndSeriesJunction_Series FOREIGN KEY (SeriesId) REFERENCES dbo.Series (Id),
    -- the series cover is this artwork's primary image
    IsCover bit NOT NULL CONSTRAINT Default_ArtworkAndSeriesJunction_IsCover DEFAULT 0
);

CREATE UNIQUE INDEX UniqueIndex_ArtworkAndSeriesJunction_Cover ON dbo.ArtworkAndSeriesJunction (SeriesId)
WHERE
    IsCover = 1;

CREATE TABLE dbo.ProductKinds (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_ProductKinds PRIMARY KEY (Id),
    Name nvarchar(50) NOT NULL,
    CONSTRAINT Unique_ProductKinds_Name UNIQUE (Name)
);

INSERT INTO
    dbo.ProductKinds (Name)
VALUES
    (N'Original'),
    (N'Print'),
    (N'Postcard');

CREATE TABLE dbo.Products (
    Id int IDENTITY(1, 1),
    CONSTRAINT PrimaryKey_Products PRIMARY KEY (Id),
    ArtworkId int NOT NULL,
    CONSTRAINT ForeignKey_Products_Artworks FOREIGN KEY (ArtworkId) REFERENCES dbo.Artworks (Id) ON DELETE CASCADE,
    ProductKindId int NOT NULL,
    CONSTRAINT ForeignKey_Products_ProductKinds FOREIGN KEY (ProductKindId) REFERENCES dbo.ProductKinds (Id),
    -- tells two products of the same kind apart, like "A4" and "A3"
    Label nvarchar(100),
    -- UNIQUE treats NULLs as equal in SQL Server, so two unlabelled prints of one artwork clash too
    CONSTRAINT Unique_Products_ArtworkKindLabel UNIQUE (ArtworkId, ProductKindId, Label),
    Price decimal(10, 2),
    CONSTRAINT Check_Products_Price CHECK (Price >= 0),
    -- how many were ever made; NULL means it can always be restocked
    EditionSize int,
    CONSTRAINT Check_Products_EditionSize CHECK (EditionSize > 0),
    Stock int NOT NULL,
    CONSTRAINT Check_Products_Stock CHECK (Stock >= 0),
    -- passes when EditionSize is NULL, for the same reason as the dimension checks
    CONSTRAINT Check_Products_StockWithinEdition CHECK (Stock <= EditionSize),
    -- only something with none left may have no price
    CONSTRAINT Check_Products_PriceUnlessSoldOut CHECK (
        Price IS NOT NULL
        OR Stock = 0
    )
);
