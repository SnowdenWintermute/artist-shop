CREATE OR ALTER FUNCTION dbo.ResolveShopItemSlug (@CandidateSlug nvarchar(200)) RETURNS nvarchar(210)
--
AS BEGIN
--
DECLARE @NumberSuffixPattern nvarchar(210) = @CandidateSlug + '-[0-9]%';

DECLARE @CandidateSlugCount int = (
    SELECT
        COUNT(*)
    FROM
        dbo.ShopItems
    WHERE
        Slug = @CandidateSlug
);

DECLARE @HighestSlugNumber int;

DECLARE @SuffixStart int = LEN(@CandidateSlug) + 2;

WITH
    NumberedSlugs AS (
        SELECT
            SUBSTRING(Slug, @SuffixStart, LEN(Slug)) AS SlugNumberText
        FROM
            dbo.ShopItems
        WHERE
            Slug LIKE @NumberSuffixPattern
    )
SELECT
    @HighestSlugNumber = MAX(TRY_CAST(SlugNumberText AS int))
FROM
    NumberedSlugs;

DECLARE @NextNumber int = COALESCE(@HighestSlugNumber, 1) + 1;

DECLARE @NumberedSlug nvarchar(210) = CONCAT(@CandidateSlug, '-', @NextNumber);

RETURN IIF(@CandidateSlugCount = 0, @CandidateSlug, @NumberedSlug);

END;
