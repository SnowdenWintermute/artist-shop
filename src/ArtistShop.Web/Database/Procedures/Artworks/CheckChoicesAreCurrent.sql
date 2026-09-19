CREATE OR ALTER PROCEDURE dbo.CheckArtworkChoicesAreCurrent @ArtworkTypeId int,
@DateCreated date,
@HeightCm decimal(8, 4),
@WidthCm decimal(8, 4),
@DepthCm decimal(8, 4),
@DurationSeconds int,
@VocabularyTermIds dbo.IdList READONLY,
@SeriesIds dbo.IdList READONLY AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

-- Everything a form offered could have been changed in another tab while it sat open. The callers
-- run this inside their own SERIALIZABLE transaction, so the rows read here stay locked until they
-- commit: nothing can switch a field off or delete a term between this check and the write.
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

-- A join at write time would silently skip a term id that no longer exists (say it was deleted in
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
) THROW 50004,
'A chosen series no longer exists.',
1;

END;
