CREATE OR ALTER FUNCTION dbo.ResolveShopItemSlug (@CandidateSlug nvarchar(200)) RETURNS nvarchar(210)
--
AS BEGIN
--
DECLARE @NumberSuffixPattern nvarchar(210) = @CandidateSlug + '-[0-9]%';

DECLARE @SuffixStart int = LEN(@CandidateSlug) + 2;

DECLARE @CandidateSlugCount int;

DECLARE @HighestSlugNumber int;

-- One read, not two. The candidate and candidate-N share a prefix, so one index seek covers both
-- and takes its locks in key order: a second caller waits rather than deadlocking.
-- UPDLOCK: with shared locks two callers could both read, then deadlock when both insert
SELECT
    @CandidateSlugCount = COUNT(
        CASE
            WHEN Slug = @CandidateSlug THEN 1
        END
    ),
    @HighestSlugNumber = MAX(
        CASE
            WHEN Slug LIKE @NumberSuffixPattern THEN TRY_CAST(SUBSTRING(Slug, @SuffixStart, LEN(Slug)) AS int)
        END
    )
FROM
    dbo.ShopItems
WITH
    (UPDLOCK)
WHERE
    Slug LIKE @CandidateSlug + '%';

DECLARE @NextNumber int = COALESCE(@HighestSlugNumber, 1) + 1;

DECLARE @NumberedSlug nvarchar(210) = CONCAT(@CandidateSlug, '-', @NextNumber);

RETURN IIF(@CandidateSlugCount = 0, @CandidateSlug, @NumberedSlug);

END;
