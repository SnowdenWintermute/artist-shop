CREATE OR ALTER PROCEDURE dbo.DeleteArtworkType @Id int AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

BEGIN TRANSACTION;

-- XLOCK takes the type row's exclusive lock now rather than at the final DELETE. AddArtwork reads
-- this row first too, so the two wait for each other here instead of deadlocking further down
DECLARE @LockedId int;

SELECT
    @LockedId = Id
FROM
    dbo.ArtworkTypes WITH (XLOCK, ROWLOCK)
WHERE
    Id = @Id;

IF EXISTS (
    SELECT
        1
    FROM
        dbo.Artworks
    WHERE
        ArtworkTypeId = @Id
) THROW 50014,
'The artwork type is used by artworks.',
1;

DELETE FROM dbo.ArtworkTypeAndArtworkFieldsJunction
WHERE
    ArtworkTypeId = @Id;

DELETE FROM dbo.VocabularyAndArtworkTypesJunction
WHERE
    ArtworkTypeId = @Id;

DELETE FROM dbo.ArtworkTypes
WHERE
    Id = @Id;

COMMIT TRANSACTION;

END;
