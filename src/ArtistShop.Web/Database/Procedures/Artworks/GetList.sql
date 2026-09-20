CREATE OR ALTER PROCEDURE dbo.GetArtworkList
@ArtworkTypeIds dbo.IdList READONLY,
@VocabularyTermIds dbo.IdList READONLY,
@MatchingArtworkIds dbo.IdList READONLY,
-- an empty list of matches means the search found nothing, which is not the same
-- as not searching at all
@IsSearching bit,
@SeriesId int,
@HasImages bit,
@IsForSale bit,
@Sort tinyint,
@Offset int,
@PageSize int AS BEGIN
SET
NOCOUNT ON;

-- must match the ArtworkListSort enum in C#
DECLARE @RecentlyAdded tinyint = 1;

DECLARE @TitleAscending tinyint = 2;

DECLARE @TitleDescending tinyint = 3;

DECLARE @DateCreatedNewest tinyint = 4;

DECLARE @DateCreatedOldest tinyint = 5;

-- how many vocabularies the chosen terms come from. An artwork has to match a term from every
-- one of them, so ticking Oil and Pastel widens the search while ticking Paper as well narrows it
DECLARE @ChosenVocabularyCount int = (
    SELECT
        COUNT(DISTINCT term.VocabularyId)
    FROM
        @VocabularyTermIds AS chosen
        JOIN dbo.VocabularyTerms AS term ON term.Id = chosen.Id
);

SELECT
    artwork.Id,
    artwork.Name,
    artworkType.Name AS ArtworkTypeName,
    artwork.DateCreated,
    artwork.DateCreatedPrecision,
    summary.ImageCount,
    summary.IsForSale,
    (
        SELECT
            STRING_AGG(series.Name, N', ') WITHIN GROUP (
                ORDER BY
                    series.SortOrder
            )
        FROM
            dbo.ArtworkAndSeriesJunction AS junction
            JOIN dbo.Series AS series ON series.Id = junction.SeriesId
        WHERE
            junction.ArtworkId = artwork.Id
    ) AS SeriesNames,
    primaryImage.StorageKey AS PrimaryImageStorageKey,
    primaryImage.Width AS PrimaryImageWidth,
    primaryImage.Height AS PrimaryImageHeight,
    primaryImage.BlurDataUri AS PrimaryImageBlurDataUri,
    -- the whole filtered count, repeated on every row of this page
    COUNT(*) OVER () AS TotalCount
FROM
    dbo.Artworks AS artwork
    JOIN dbo.ArtworkTypes AS artworkType ON artworkType.Id = artwork.ArtworkTypeId
    -- UniqueIndex_ArtworkImages_Primary allows one primary row per artwork, so this join can
    -- only ever add one row. The same rule GetSeriesWithCovers and GetSeries read a cover by
    LEFT JOIN dbo.ArtworkImages AS primaryImage ON primaryImage.ArtworkId = artwork.Id
    AND primaryImage.IsPrimary = 1
    -- worked out once, for the columns above and the filters below. APPLY is part of FROM,
    -- so unlike a column alias its results can be used in WHERE
    CROSS APPLY (
        SELECT
            (
                SELECT
                    COUNT(*)
                FROM
                    dbo.ArtworkImages AS image
                WHERE
                    image.ArtworkId = artwork.Id
            ) AS ImageCount,
            CAST(
                CASE
                    WHEN EXISTS (
                        SELECT
                            1
                        FROM
                            dbo.Products AS product
                        WHERE
                            product.ArtworkId = artwork.Id
                            AND product.Stock >= 1
                    ) THEN 1
                    ELSE 0
                END AS bit
            ) AS IsForSale
    ) AS summary
WHERE
    (
        NOT EXISTS (
            SELECT
                1
            FROM
                @ArtworkTypeIds
        )
        OR artwork.ArtworkTypeId IN (
            SELECT
                Id
            FROM
                @ArtworkTypeIds
        )
    )
    AND (
        @SeriesId IS NULL
        OR EXISTS (
            SELECT
                1
            FROM
                dbo.ArtworkAndSeriesJunction AS junction
            WHERE
                junction.ArtworkId = artwork.Id
                AND junction.SeriesId = @SeriesId
        )
    )
    AND (
        @IsSearching = 0
        OR artwork.Id IN (
            SELECT
                Id
            FROM
                @MatchingArtworkIds
        )
    )
    AND (
        @HasImages IS NULL
        OR @HasImages = CASE
            WHEN summary.ImageCount > 0 THEN 1
            ELSE 0
        END
    )
    AND (
        @IsForSale IS NULL
        OR @IsForSale = summary.IsForSale
    )
    AND (
        @ChosenVocabularyCount = 0
        -- the junction carries VocabularyId of its own, so counting the vocabularies an
        -- artwork matches needs no join back to the terms
        OR (
            SELECT
                COUNT(DISTINCT junction.VocabularyId)
            FROM
                dbo.ArtworkAndVocabularyTermsJunction AS junction
            WHERE
                junction.ArtworkId = artwork.Id
                AND junction.TermId IN (
                    SELECT
                        Id
                    FROM
                        @VocabularyTermIds
                )
        ) = @ChosenVocabularyCount
    )
ORDER BY
    -- an undated artwork sinks to the bottom of either date sort rather than leading
    -- the oldest-first one. Every row gets 0 under the other sorts, so it does nothing there
    CASE
        WHEN @Sort IN (@DateCreatedNewest, @DateCreatedOldest)
        AND artwork.DateCreated IS NULL THEN 1
        ELSE 0
    END,
    -- only the chosen sort's CASE has a value; the rest are NULL for every row, which sorts
    -- everything equal and so changes nothing
    CASE
        WHEN @Sort = @RecentlyAdded THEN artwork.CreatedAt
    END DESC,
    CASE
        WHEN @Sort = @TitleAscending THEN artwork.Name
    END,
    CASE
        WHEN @Sort = @TitleDescending THEN artwork.Name
    END DESC,
    CASE
        WHEN @Sort = @DateCreatedNewest THEN artwork.DateCreated
    END DESC,
    CASE
        WHEN @Sort = @DateCreatedOldest THEN artwork.DateCreated
    END,
    -- the tie-break that stops a row moving between pages
    artwork.Id OFFSET @Offset ROWS
FETCH NEXT
    @PageSize ROWS ONLY;

END;
