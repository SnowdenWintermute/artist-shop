CREATE OR ALTER PROCEDURE dbo.AddArtwork @ArtworkTypeId int,
@Name nvarchar(200),
@CandidateSlug nvarchar(200),
@Description nvarchar(max),
@DateCreated date,
@DateCreatedPrecision tinyint,
@HeightCm decimal(8, 4),
@WidthCm decimal(8, 4),
@DepthCm decimal(8, 4),
@DurationSeconds int,
@Images dbo.ArtworkImageList READONLY,
@VocabularyTermIds dbo.IdList READONLY,
@SeriesIds dbo.IdList READONLY,
@Products dbo.ProductList READONLY AS BEGIN
-- Stops SQL Server emitting a "(1 row affected)" message per statement. Those
-- are extra results the client has to skip past, and they confuse some drivers.
SET
NOCOUNT ON;

-- makes the "batch" abort if error and roll transaction back
SET
XACT_ABORT ON;

-- a session setting, so it holds for the nested procedures too: the rows they read stay locked
-- until this transaction commits
SET
TRANSACTION ISOLATION LEVEL SERIALIZABLE;

BEGIN TRANSACTION;

EXEC dbo.CheckArtworkChoicesAreCurrent @ArtworkTypeId = @ArtworkTypeId,
@DateCreated = @DateCreated,
@HeightCm = @HeightCm,
@WidthCm = @WidthCm,
@DepthCm = @DepthCm,
@DurationSeconds = @DurationSeconds,
@VocabularyTermIds = @VocabularyTermIds,
@SeriesIds = @SeriesIds;

-- Not in the shared check, because only this procedure takes products: the CSV import is the one
-- caller that sends any today. It moves there once the add and edit forms post them as well.
IF EXISTS (
    SELECT
        1
    FROM
        @Products AS product
    WHERE
        NOT EXISTS (
            SELECT
                1
            FROM
                dbo.ProductTypes AS productType
            WHERE
                productType.Id = product.ProductTypeId
        )
) THROW 50012,
'A chosen product type no longer exists.',
1;

DECLARE @Slug nvarchar(210) = dbo.ResolveArtworkSlug (@CandidateSlug);

INSERT INTO
    dbo.Artworks (
        ArtworkTypeId,
        Name,
        Slug,
        Description,
        DateCreated,
        DateCreatedPrecision,
        HeightCm,
        WidthCm,
        DepthCm,
        DurationSeconds
    )
VALUES
    (
        @ArtworkTypeId,
        @Name,
        @Slug,
        @Description,
        @DateCreated,
        @DateCreatedPrecision,
        @HeightCm,
        @WidthCm,
        @DepthCm,
        @DurationSeconds
    );

-- it assigns @Id to the most recently created IDENTITY
-- in the current scope (batch, procedure, function or trigger)
DECLARE @Id int = SCOPE_IDENTITY();

EXEC dbo.SetArtworkImages @ArtworkId = @Id,
@Images = @Images;

EXEC dbo.SetArtworkVocabularyTerms @ArtworkId = @Id,
@ArtworkTypeId = @ArtworkTypeId,
@VocabularyTermIds = @VocabularyTermIds;

EXEC dbo.SetArtworkSeries @ArtworkId = @Id,
@SeriesIds = @SeriesIds;

INSERT INTO
    dbo.Products (ArtworkId, ProductTypeId, Label, Price, EditionSize, Stock)
SELECT
    @Id,
    ProductTypeId,
    Label,
    Price,
    EditionSize,
    Stock
FROM
    @Products;

COMMIT TRANSACTION;

SELECT
    @Id AS Id,
    @Slug AS Slug;

END;
