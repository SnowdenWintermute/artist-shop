CREATE OR ALTER PROCEDURE dbo.GetArtworkNames @ArtworkTypeId int AS BEGIN
SET
NOCOUNT ON;

-- one work type's titles. A photograph and a screenshot can share a title: they are different
-- works, and only a title this type already has is a repeat. The same rule the bulk image
-- uploader matches file names by
SELECT
    Name
FROM
    dbo.Artworks
WHERE
    ArtworkTypeId = @ArtworkTypeId;

END;
