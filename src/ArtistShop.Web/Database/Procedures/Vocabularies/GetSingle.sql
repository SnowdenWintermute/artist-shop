CREATE OR ALTER PROCEDURE dbo.GetVocabulary @Id int AS BEGIN
SET
NOCOUNT ON;

SELECT
    Id,
    Name
FROM
    dbo.Vocabularies
WHERE
    Id = @Id;

SELECT
    ArtworkTypeId
FROM
    dbo.VocabularyAndArtworkTypesJunction
WHERE
    VocabularyId = @Id;

END;
