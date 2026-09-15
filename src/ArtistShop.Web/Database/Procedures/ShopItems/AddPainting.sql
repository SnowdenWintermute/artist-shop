CREATE OR ALTER PROCEDURE dbo.AddPainting @Name nvarchar(200),
@CandidateSlug nvarchar(200),
@Price decimal(10, 2),
@Stock int,
@DatePainted date,
@DatePaintedPrecision tinyint,
@Description nvarchar(max),
@WidthCm decimal(8, 4),
@HeightCm decimal(8, 4),
@Images dbo.ShopItemImageList READONLY,
@VocabularyTermIds dbo.IdList READONLY,
@SeriesIds dbo.IdList READONLY AS BEGIN
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

DECLARE @PaintingShopItemTypeId int = 1;

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

DECLARE @Slug nvarchar(210) = dbo.ResolveShopItemSlug (@CandidateSlug);

INSERT INTO
    dbo.ShopItems (Name, ShopItemTypeId, Slug, Price, Stock)
VALUES
    (@Name, @PaintingShopItemTypeId, @Slug, @Price, @Stock);

DECLARE @Id int;

-- it assigns @Id to the most recently created IDENTITY
-- in the current scope (batch, procedure, function or trigger)
SET
    @Id = SCOPE_IDENTITY();

INSERT INTO
    dbo.Paintings (Id, DatePainted, DatePaintedPrecision, Description, WidthCm, HeightCm)
VALUES
    (@Id, @DatePainted, @DatePaintedPrecision, @Description, @WidthCm, @HeightCm);

-- inserts all the rows in the table returned from the select
INSERT INTO
    dbo.ShopItemImages (
        ShopItemId,
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

-- VocabularyId is looked up from each term rather than passed in, and ShopItemTypeId is
-- copied in, so the three foreign keys on the junction can check the row. If a term's
-- vocabulary isn't ticked for paintings, the insert fails with error 547.
INSERT INTO
    dbo.ShopItemAndVocabularyTermsJunction (ShopItemId, ShopItemTypeId, TermId, VocabularyId)
SELECT
    @Id,
    @PaintingShopItemTypeId,
    term.Id,
    term.VocabularyId
FROM
    @VocabularyTermIds AS termIds
    JOIN dbo.VocabularyTerms AS term ON term.Id = termIds.Id;

-- the column is SeriesId but the source column is just Id
INSERT INTO
    dbo.ShopItemAndSeriesJunction (ShopItemId, SeriesId, SortOrder)
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
                dbo.ShopItemAndSeriesJunction AS existing
            WITH
                (UPDLOCK)
            WHERE
                existing.SeriesId = seriesIds.Id
        ),
        -1
    ) + 1
FROM
    @SeriesIds AS seriesIds;

COMMIT TRANSACTION;

SELECT
    @Id AS Id,
    @Slug AS Slug;

END;
