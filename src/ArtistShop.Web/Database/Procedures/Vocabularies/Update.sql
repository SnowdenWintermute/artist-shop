CREATE OR ALTER PROCEDURE dbo.UpdateVocabulary @Id int,
@Name nvarchar(100),
@ArtworkTypeIds dbo.IdList READONLY AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

BEGIN TRANSACTION;

UPDATE dbo.Vocabularies
SET
    Name = @Name
WHERE
    Id = @Id;

IF @@ROWCOUNT = 0 THROW 50002,
'The vocabulary no longer exists.',
1;

-- VocabularyTerms must be removed from Artworks first; the foreign key refuses removing a vocabularyArtworkAssociation that's still in use
DELETE vocabularyTermArtworkAssociation
FROM
    dbo.ArtworkAndVocabularyTermsJunction AS vocabularyTermArtworkAssociation
WHERE
    vocabularyTermArtworkAssociation.VocabularyId = @Id
    AND NOT EXISTS (
        SELECT
            1
        FROM
            @ArtworkTypeIds AS artworkTypeIds
        WHERE
            artworkTypeIds.Id = vocabularyTermArtworkAssociation.ArtworkTypeId
    );

DELETE vocabularyArtworkAssociation
FROM
    dbo.VocabularyAndArtworkTypesJunction AS vocabularyArtworkAssociation
WHERE
    vocabularyArtworkAssociation.VocabularyId = @Id
    AND NOT EXISTS (
        SELECT
            1
        FROM
            @ArtworkTypeIds AS artworkTypeIds
        WHERE
            artworkTypeIds.Id = vocabularyArtworkAssociation.ArtworkTypeId
    );

-- the join drops a type deleted in another tab, as in AddVocabulary
INSERT INTO
    dbo.VocabularyAndArtworkTypesJunction (VocabularyId, ArtworkTypeId)
SELECT
    @Id,
    artworkType.Id
FROM
    @ArtworkTypeIds AS artworkTypeIds
    JOIN dbo.ArtworkTypes AS artworkType ON artworkType.Id = artworkTypeIds.Id
WHERE
    NOT EXISTS (
        SELECT
            1
        FROM
            dbo.VocabularyAndArtworkTypesJunction AS existing
        WHERE
            existing.VocabularyId = @Id
            AND existing.ArtworkTypeId = artworkTypeIds.Id
    );

COMMIT TRANSACTION;

END;
