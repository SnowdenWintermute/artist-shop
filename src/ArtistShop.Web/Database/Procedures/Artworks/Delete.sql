CREATE OR ALTER PROCEDURE dbo.DeleteArtwork @Id int AS BEGIN
SET
NOCOUNT ON;

-- Every table that references an artwork cascades, so this one statement also removes its images,
-- its series and vocabulary term rows and its products. No transaction: a single DELETE is already
-- all or nothing. The image files stay until OrphanedImageSweeper finds no row pointing at them,
-- which is the same path an abandoned upload takes.
DELETE FROM dbo.Artworks
WHERE
    Id = @Id;

END;
