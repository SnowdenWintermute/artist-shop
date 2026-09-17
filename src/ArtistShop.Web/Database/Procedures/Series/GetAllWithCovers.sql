CREATE OR ALTER PROCEDURE dbo.GetSeriesWithCovers AS BEGIN
SET
NOCOUNT ON;

SELECT
    series.Id,
    series.Name,
    series.Slug,
    (
        SELECT
            COUNT(*)
        FROM
            dbo.ArtworkAndSeriesJunction AS junction
        WHERE
            junction.SeriesId = series.Id
    ) AS ArtworkCount,
    cover.StorageKey AS CoverStorageKey,
    cover.OriginalFileName AS CoverOriginalFileName,
    cover.Width AS CoverWidth,
    cover.Height AS CoverHeight,
    cover.BlurDataUri AS CoverBlurDataUri
FROM
    dbo.Series AS series
    -- OUTER APPLY runs the subquery once per series. Like a LEFT JOIN it keeps series with no
    -- match, but unlike a join the subquery can use TOP and ORDER BY
    OUTER APPLY (
        SELECT
            TOP (1) primaryImage.StorageKey,
            primaryImage.OriginalFileName,
            primaryImage.Width,
            primaryImage.Height,
            primaryImage.BlurDataUri
        FROM
            dbo.ArtworkAndSeriesJunction AS junction
            -- an inner join, so artworks with no images can't become the cover
            JOIN dbo.ArtworkImages AS primaryImage ON primaryImage.ArtworkId = junction.ArtworkId
            AND primaryImage.IsPrimary = 1
        WHERE
            junction.SeriesId = series.Id
        ORDER BY
            junction.IsCover DESC,
            junction.SortOrder
    ) AS cover
ORDER BY
    series.SortOrder;

END;
