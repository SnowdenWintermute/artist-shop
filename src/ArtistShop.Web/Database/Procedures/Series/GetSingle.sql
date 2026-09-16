CREATE OR ALTER PROCEDURE dbo.GetSeries @Id int AS BEGIN
SET
NOCOUNT ON;

SELECT
    Id,
    Name,
    Slug
FROM
    dbo.Series
WHERE
    Id = @Id;

SELECT
    artwork.Id,
    artwork.Name,
    artworkType.Name AS ArtworkTypeName,
    junction.IsCover,
    primaryImage.RelativePath,
    primaryImage.OriginalFileName,
    primaryImage.Width,
    primaryImage.Height,
    primaryImage.BlurDataUri
FROM
    dbo.ArtworkAndSeriesJunction AS junction
    JOIN dbo.Artworks AS artwork ON artwork.Id = junction.ArtworkId
    JOIN dbo.ArtworkTypes AS artworkType ON artworkType.Id = artwork.ArtworkTypeId
    LEFT JOIN dbo.ArtworkImages AS primaryImage ON primaryImage.ArtworkId = artwork.Id
    AND primaryImage.IsPrimary = 1
WHERE
    junction.SeriesId = @Id
ORDER BY
    junction.SortOrder;

END;
