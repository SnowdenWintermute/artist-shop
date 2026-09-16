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

SET
TRANSACTION ISOLATION LEVEL SERIALIZABLE;

BEGIN TRANSACTION;

-- must match the ArtworkField enum in C#
DECLARE @DateCreatedFieldId int = 1;

DECLARE @HeightAndWidthFieldId int = 2;

DECLARE @DepthFieldId int = 3;

DECLARE @DurationFieldId int = 4;

IF NOT EXISTS (
    SELECT
        1
    FROM
        dbo.ArtworkTypes
    WHERE
        Id = @ArtworkTypeId
) THROW 50010,
'The artwork type no longer exists.',
1;

-- a table variable: a temporary table that lives until the procedure ends
DECLARE @EnabledFieldIds TABLE (Id int PRIMARY KEY);

-- under SERIALIZABLE, the rows read here stay locked until COMMIT, so another tab can't switch a
-- field off between this check and the insert
INSERT INTO
    @EnabledFieldIds (Id)
SELECT
    ArtworkFieldId
FROM
    dbo.ArtworkTypeAndArtworkFieldsJunction
WHERE
    ArtworkTypeId = @ArtworkTypeId;

-- a value for a field the type doesn't have means it was switched off while the form was open
IF (
    @DateCreated IS NOT NULL
    AND NOT EXISTS (
        SELECT
            1
        FROM
            @EnabledFieldIds
        WHERE
            Id = @DateCreatedFieldId
    )
)
OR (
    COALESCE(@HeightCm, @WidthCm) IS NOT NULL
    AND NOT EXISTS (
        SELECT
            1
        FROM
            @EnabledFieldIds
        WHERE
            Id = @HeightAndWidthFieldId
    )
)
OR (
    @DepthCm IS NOT NULL
    AND NOT EXISTS (
        SELECT
            1
        FROM
            @EnabledFieldIds
        WHERE
            Id = @DepthFieldId
    )
)
OR (
    @DurationSeconds IS NOT NULL
    AND NOT EXISTS (
        SELECT
            1
        FROM
            @EnabledFieldIds
        WHERE
            Id = @DurationFieldId
    )
) THROW 50011,
'A field was switched off for this artwork type.',
1;

-- A join below would silently skip a term id that no longer exists (say it was deleted in
-- another tab while this form was open). Checking first turns that into a loud error.
-- THROW needs the statement before it to end with a semicolon.
IF EXISTS (
    SELECT
        1
    FROM
        @VocabularyTermIds AS vocabularyTermIds
    WHERE
        NOT EXISTS (
            SELECT
                1
            FROM
                dbo.VocabularyTerms AS vocabularyTerm
            WHERE
                vocabularyTerm.Id = vocabularyTermIds.Id
        )
)
-- 50000 and above are free for our own errors. With XACT_ABORT ON, THROW also
-- rolls the transaction back.
THROW 50001,
'A chosen vocabulary term no longer exists.',
1;

-- the junction's foreign key would catch a deleted series too, but as error 547, which says
-- nothing about which choice was stale
IF EXISTS (
    SELECT
        1
    FROM
        @SeriesIds AS seriesIds
    WHERE
        NOT EXISTS (
            SELECT
                1
            FROM
                dbo.Series AS series
            WHERE
                series.Id = seriesIds.Id
        )
) THROW 50009,
'A chosen series no longer exists.',
1;

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
                dbo.ProductKinds AS productKind
            WHERE
                productKind.Id = product.ProductKindId
        )
) THROW 50012,
'A chosen product kind no longer exists.',
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

-- inserts all the rows in the table returned from the select
INSERT INTO
    dbo.ArtworkImages (
        ArtworkId,
        RelativePath,
        OriginalFileName,
        SortOrder,
        IsPrimary,
        Width,
        Height,
        BlurDataUri
    )
SELECT
    @Id,
    RelativePath,
    OriginalFileName,
    SortOrder,
    IsPrimary,
    Width,
    Height,
    BlurDataUri
FROM
    @Images;

-- VocabularyId is looked up from each term rather than passed in, and ArtworkTypeId is
-- copied in, so the three foreign keys on the junction can check the row. If a term's
-- vocabulary isn't ticked for this type, the insert fails with error 547.
INSERT INTO
    dbo.ArtworkAndVocabularyTermsJunction (ArtworkId, ArtworkTypeId, TermId, VocabularyId)
SELECT
    @Id,
    @ArtworkTypeId,
    term.Id,
    term.VocabularyId
FROM
    @VocabularyTermIds AS termIds
    JOIN dbo.VocabularyTerms AS term ON term.Id = termIds.Id;

-- the column is SeriesId but the source column is just Id
INSERT INTO
    dbo.ArtworkAndSeriesJunction (ArtworkId, SeriesId, SortOrder)
SELECT
    @Id,
    seriesIds.Id,
    -- a correlated subquery: it runs once per row of @SeriesIds, with seriesIds.Id
    -- filled in from the outer row. MAX over no rows is NULL, so COALESCE turns
    -- "empty series" into -1, which the + 1 makes 0
    COALESCE(
        (
            SELECT
                MAX(existing.SortOrder)
            FROM
                dbo.ArtworkAndSeriesJunction AS existing
            WITH
                (UPDLOCK)
            WHERE
                existing.SeriesId = seriesIds.Id
        ),
        -1
    ) + 1
FROM
    @SeriesIds AS seriesIds;

INSERT INTO
    dbo.Products (ArtworkId, ProductKindId, Label, Price, EditionSize, Stock)
SELECT
    @Id,
    ProductKindId,
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
