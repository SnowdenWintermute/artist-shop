CREATE OR ALTER PROCEDURE dbo.GetArtworkNameMatches @ArtworkTypeId int,
@ArtworkNames dbo.ArtworkNameList READONLY AS BEGIN
SET
NOCOUNT ON;

-- must match the ArtworkNameMatchType enum in C#, and the same rules as
-- AttachPrimaryImageToImagelessArtworkByName
DECLARE @OneImagelessArtwork tinyint = 1;

DECLARE @NoArtwork tinyint = 2;

DECLARE @SeveralArtworks tinyint = 3;

DECLARE @ArtworkWithImages tinyint = 4;

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

-- A CTE (common table expression) is a named query that the next statement can use like a table.
-- LEFT JOIN keeps a name that matches nothing, as one row with a NULL ArtworkId
WITH
    NameMatches AS (
        SELECT
            artworkName.Name,
            artwork.Id AS ArtworkId,
            CASE
                WHEN EXISTS (
                    SELECT
                        1
                    FROM
                        dbo.ArtworkImages AS artworkImage
                    WHERE
                        artworkImage.ArtworkId = artwork.Id
                ) THEN 1
                ELSE 0
            END AS HasImages
        FROM
            @ArtworkNames AS artworkName
            LEFT JOIN dbo.Artworks AS artwork ON artwork.ArtworkTypeId = @ArtworkTypeId
            AND artwork.Name = artworkName.Name
    )
SELECT
    Name,
    -- COUNT of a column skips NULLs, so an unmatched name counts 0
    CASE
        WHEN COUNT(ArtworkId) = 0 THEN @NoArtwork
        WHEN COUNT(ArtworkId) > 1 THEN @SeveralArtworks
        WHEN MAX(HasImages) = 1 THEN @ArtworkWithImages
        ELSE @OneImagelessArtwork
    END AS MatchType
FROM
    NameMatches
GROUP BY
    Name;

-- every match, so the report can link to them
SELECT
    artworkName.Name,
    artwork.Id AS ArtworkId
FROM
    @ArtworkNames AS artworkName
    JOIN dbo.Artworks AS artwork ON artwork.ArtworkTypeId = @ArtworkTypeId
    AND artwork.Name = artworkName.Name;

END;
