CREATE OR ALTER PROCEDURE dbo.UpdateArtworkType @Id int,
@Name nvarchar(50),
@ArtworkFieldIds dbo.IdList READONLY AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

BEGIN TRANSACTION;

-- must match the ArtworkField enum in C#
DECLARE @DateCreatedFieldId int = 1;

DECLARE @HeightAndWidthFieldId int = 2;

DECLARE @DepthFieldId int = 3;

DECLARE @DurationFieldId int = 4;

-- first, so this locks the type row before anything else, in the same order as AddArtwork
UPDATE dbo.ArtworkTypes
SET
    Name = @Name
WHERE
    Id = @Id;

IF @@ROWCOUNT = 0 THROW 50010,
'The artwork type no longer exists.',
1;

-- a switched-off field's values are cleared, so an artwork only holds values for its type's fields.
-- Depth before height and width: Check_Artworks_DepthNeedsHeightAndWidth checks each row as it changes
IF NOT EXISTS (
    SELECT
        1
    FROM
        @ArtworkFieldIds
    WHERE
        Id = @DepthFieldId
)
UPDATE dbo.Artworks
SET
    DepthCm = NULL
WHERE
    ArtworkTypeId = @Id
    AND DepthCm IS NOT NULL;

IF NOT EXISTS (
    SELECT
        1
    FROM
        @ArtworkFieldIds
    WHERE
        Id = @HeightAndWidthFieldId
)
UPDATE dbo.Artworks
SET
    HeightCm = NULL,
    WidthCm = NULL
WHERE
    ArtworkTypeId = @Id
    AND HeightCm IS NOT NULL;

IF NOT EXISTS (
    SELECT
        1
    FROM
        @ArtworkFieldIds
    WHERE
        Id = @DateCreatedFieldId
)
UPDATE dbo.Artworks
SET
    DateCreated = NULL,
    DateCreatedPrecision = NULL
WHERE
    ArtworkTypeId = @Id
    AND DateCreated IS NOT NULL;

IF NOT EXISTS (
    SELECT
        1
    FROM
        @ArtworkFieldIds
    WHERE
        Id = @DurationFieldId
)
UPDATE dbo.Artworks
SET
    DurationSeconds = NULL
WHERE
    ArtworkTypeId = @Id
    AND DurationSeconds IS NOT NULL;

-- one statement each: SQL Server checks a self-referencing foreign key after the whole statement,
-- so depth and height and width can go (or arrive) together
DELETE junction
FROM
    dbo.ArtworkTypeAndArtworkFieldsJunction AS junction
WHERE
    junction.ArtworkTypeId = @Id
    AND NOT EXISTS (
        SELECT
            1
        FROM
            @ArtworkFieldIds AS artworkFieldIds
        WHERE
            artworkFieldIds.Id = junction.ArtworkFieldId
    );

INSERT INTO
    dbo.ArtworkTypeAndArtworkFieldsJunction (ArtworkTypeId, ArtworkFieldId, RequiresArtworkFieldId)
SELECT
    @Id,
    artworkField.Id,
    artworkField.RequiresArtworkFieldId
FROM
    @ArtworkFieldIds AS artworkFieldIds
    JOIN dbo.ArtworkFields AS artworkField ON artworkField.Id = artworkFieldIds.Id
WHERE
    NOT EXISTS (
        SELECT
            1
        FROM
            dbo.ArtworkTypeAndArtworkFieldsJunction AS existing
        WHERE
            existing.ArtworkTypeId = @Id
            AND existing.ArtworkFieldId = artworkField.Id
    );

COMMIT TRANSACTION;

END;
