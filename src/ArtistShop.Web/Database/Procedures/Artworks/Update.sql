CREATE OR ALTER PROCEDURE dbo.UpdateArtwork @Id int,
@Name nvarchar(200),
@CandidateSlug nvarchar(200),
@Description nvarchar(max),
@DateCreated date,
@DateCreatedPrecision tinyint,
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

SET
TRANSACTION ISOLATION LEVEL SERIALIZABLE;

BEGIN TRANSACTION;

-- An artwork's type never changes, so it isn't a parameter: it's read from the row, which the
-- vocabulary junction copies and which decides the allowed fields. UPDLOCK holds that row from here
-- until COMMIT, so a delete in another tab waits rather than landing between this read and the UPDATE.
DECLARE @ArtworkTypeId int;

DECLARE @CurrentSlug nvarchar(210);

SELECT
    @ArtworkTypeId = ArtworkTypeId,
    @CurrentSlug = Slug
FROM
    dbo.Artworks WITH (UPDLOCK, ROWLOCK)
WHERE
    Id = @Id;

-- no row leaves both variables unset, and neither column is nullable
IF @ArtworkTypeId IS NULL THROW 50003,
'The artwork no longer exists.',
1;

EXEC dbo.CheckArtworkChoicesAreCurrent @ArtworkTypeId = @ArtworkTypeId,
@DateCreated = @DateCreated,
@HeightCm = @HeightCm,
@WidthCm = @WidthCm,
@DepthCm = @DepthCm,
@DurationSeconds = @DurationSeconds,
@VocabularyTermIds = @VocabularyTermIds,
@SeriesIds = @SeriesIds;

-- Keep the slug when it already belongs to this name's family, so saving an unchanged form doesn't
-- move sunset-2 to sunset-4. The pattern alone would also accept sunset-2b, which TRY_CAST rules
-- out by returning NULL when what follows the dash isn't a whole number.
DECLARE @NumberSuffixPattern nvarchar(210) = @CandidateSlug + '-[0-9]%';

DECLARE @CurrentSlugNumber int = TRY_CAST(
    SUBSTRING(@CurrentSlug, LEN(@CandidateSlug) + 2, LEN(@CurrentSlug)) AS int
);

DECLARE @Slug nvarchar(210) = CASE
    WHEN @CurrentSlug = @CandidateSlug THEN @CurrentSlug
    WHEN @CurrentSlug LIKE @NumberSuffixPattern
    AND @CurrentSlugNumber IS NOT NULL THEN @CurrentSlug
    -- the current slug is outside the family, so this artwork's own row can't be what makes
    -- ResolveArtworkSlug add a number
    ELSE dbo.ResolveArtworkSlug (@CandidateSlug)
END;

UPDATE dbo.Artworks
SET
    Name = @Name,
    Slug = @Slug,
    Description = @Description,
    DateCreated = @DateCreated,
    DateCreatedPrecision = @DateCreatedPrecision,
    HeightCm = @HeightCm,
    WidthCm = @WidthCm,
    DepthCm = @DepthCm,
    DurationSeconds = @DurationSeconds
WHERE
    Id = @Id;

EXEC dbo.SetArtworkVocabularyTerms @ArtworkId = @Id,
@ArtworkTypeId = @ArtworkTypeId,
@VocabularyTermIds = @VocabularyTermIds;

EXEC dbo.SetArtworkSeries @ArtworkId = @Id,
@SeriesIds = @SeriesIds;

COMMIT TRANSACTION;

SELECT
    @Slug AS Slug;

END;
