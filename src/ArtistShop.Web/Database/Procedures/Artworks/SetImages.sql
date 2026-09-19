CREATE OR ALTER PROCEDURE dbo.SetArtworkImages @ArtworkId int,
@Images dbo.ArtworkImageList READONLY AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

-- The form posts the whole list every time, in the order the artist put it in, so replacing every
-- row is simpler than working out which moved. Nothing refers to an ArtworkImages row by its Id,
-- so a kept image getting a new one costs nothing. On a new artwork the DELETE finds nothing.
DELETE FROM dbo.ArtworkImages
WHERE
    ArtworkId = @ArtworkId;

-- a removed image's file is left behind with no row pointing at it, which is what
-- OrphanedImageSweeper looks for
INSERT INTO
    dbo.ArtworkImages (
        ArtworkId,
        StorageKey,
        OriginalFileName,
        SortOrder,
        IsPrimary,
        Width,
        Height,
        BlurDataUri
    )
SELECT
    @ArtworkId,
    StorageKey,
    OriginalFileName,
    SortOrder,
    IsPrimary,
    Width,
    Height,
    BlurDataUri
FROM
    @Images;

END;
