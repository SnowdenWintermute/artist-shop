CREATE OR ALTER PROCEDURE dbo.GetArtworkBySlug @Slug nvarchar(200) AS BEGIN
SET
NOCOUNT ON;

DECLARE @Id int = (
    SELECT
        Id
    FROM
        dbo.Artworks
    WHERE
        Slug = @Slug
);

-- the called procedure's result sets go straight to our caller. An unknown slug leaves @Id NULL,
-- which matches no rows, so every result set comes back empty
EXEC dbo.GetArtworkById @Id = @Id;

END;
