CREATE OR ALTER PROCEDURE dbo.GetArtworkNeighboursInSeries @SeriesId int,
@ArtworkId int,
@OnlyArtworksWithImages bit AS BEGIN
SET
NOCOUNT ON;

-- The artwork's own place in the series is the anchor, so both sides come off one row and an
-- artwork that isn't in the series returns nothing at all. UNIQUE (SeriesId, SortOrder) means no
-- two artworks share a place, so < and > can't step over one
SELECT
    previousArtwork.Name AS PreviousName,
    previousArtwork.Slug AS PreviousSlug,
    nextArtwork.Name AS NextName,
    nextArtwork.Slug AS NextSlug
FROM
    dbo.ArtworkAndSeriesJunction AS here
    OUTER APPLY (
        SELECT
            TOP (1) artwork.Name,
            artwork.Slug
        FROM
            dbo.ArtworkAndSeriesJunction AS junction
            JOIN dbo.Artworks AS artwork ON artwork.Id = junction.ArtworkId
        WHERE
            junction.SeriesId = here.SeriesId
            AND junction.SortOrder < here.SortOrder
            -- a work with no photograph is not somewhere a visitor can be sent
            AND (
                @OnlyArtworksWithImages = 0
                OR EXISTS (
                    SELECT
                        1
                    FROM
                        dbo.ArtworkImages AS image
                    WHERE
                        image.ArtworkId = artwork.Id
                )
            )
        ORDER BY
            junction.SortOrder DESC
    ) AS previousArtwork
    OUTER APPLY (
        SELECT
            TOP (1) artwork.Name,
            artwork.Slug
        FROM
            dbo.ArtworkAndSeriesJunction AS junction
            JOIN dbo.Artworks AS artwork ON artwork.Id = junction.ArtworkId
        WHERE
            junction.SeriesId = here.SeriesId
            AND junction.SortOrder > here.SortOrder
            AND (
                @OnlyArtworksWithImages = 0
                OR EXISTS (
                    SELECT
                        1
                    FROM
                        dbo.ArtworkImages AS image
                    WHERE
                        image.ArtworkId = artwork.Id
                )
            )
        ORDER BY
            junction.SortOrder
    ) AS nextArtwork
WHERE
    here.SeriesId = @SeriesId
    AND here.ArtworkId = @ArtworkId;

END;
