CREATE OR ALTER PROCEDURE dbo.AttachPrimaryImageToImagelessArtworkByName @ArtworkTypeId int,
@ArtworkName nvarchar(200),
@RelativePath nvarchar(400),
@OriginalFileName nvarchar(260),
@Width int,
@Height int,
@BlurDataUri nvarchar(1000) AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

-- must match the ArtworkNameMatchType enum in C#, and the same rules as GetArtworkNameMatches.
-- OneImagelessArtwork here means the image was attached to it
DECLARE @OneImagelessArtwork tinyint = 1;

DECLARE @NoArtwork tinyint = 2;

DECLARE @SeveralArtworks tinyint = 3;

DECLARE @ArtworkWithImages tinyint = 4;

BEGIN TRANSACTION;

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

DECLARE @MatchingArtworkIds TABLE (Id int PRIMARY KEY);

-- The column's collation (pinned in DatabaseInitializer) makes = ignore case, so "sunset"
-- matches "Sunset". UPDLOCK holds the matched rows until COMMIT: a second upload for the
-- same artwork waits here, then sees the image this one inserted.
INSERT INTO
    @MatchingArtworkIds (Id)
SELECT
    Id
FROM
    dbo.Artworks
WITH
    (UPDLOCK)
WHERE
    ArtworkTypeId = @ArtworkTypeId
    AND Name = @ArtworkName;

DECLARE @MatchCount int = (
    SELECT
        COUNT(*)
    FROM
        @MatchingArtworkIds
);

DECLARE @MatchType tinyint;

IF @MatchCount = 0
SET
    @MatchType = @NoArtwork;

ELSE IF @MatchCount > 1
SET
    @MatchType = @SeveralArtworks;

ELSE IF EXISTS (
    SELECT
        1
    FROM
        dbo.ArtworkImages AS artworkImage
        JOIN @MatchingArtworkIds AS matchingArtworkIds ON matchingArtworkIds.Id = artworkImage.ArtworkId
)
SET
    @MatchType = @ArtworkWithImages;

ELSE BEGIN
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
    Id,
    @RelativePath,
    @OriginalFileName,
    0,
    1,
    @Width,
    @Height,
    @BlurDataUri
FROM
    @MatchingArtworkIds;

SET
    @MatchType = @OneImagelessArtwork;

END;

COMMIT TRANSACTION;

SELECT
    @MatchType AS MatchType;

-- every match, so the report can link to them
SELECT
    Id
FROM
    @MatchingArtworkIds;

END;
