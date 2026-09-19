CREATE OR ALTER PROCEDURE dbo.SetArtworkVocabularyTerms @ArtworkId int,
@ArtworkTypeId int,
@VocabularyTermIds dbo.IdList READONLY AS BEGIN
SET
NOCOUNT ON;

SET
XACT_ABORT ON;

-- A term row carries nothing of its own, so replacing all of them is simpler than working out a
-- difference. On a new artwork the DELETE finds nothing and this is only the insert.
DELETE FROM dbo.ArtworkAndVocabularyTermsJunction
WHERE
    ArtworkId = @ArtworkId;

-- VocabularyId is looked up from each term rather than passed in, and ArtworkTypeId is copied in,
-- so the three foreign keys on the junction can check the row. If a term's vocabulary isn't ticked
-- for this type, the insert fails with error 547.
INSERT INTO
    dbo.ArtworkAndVocabularyTermsJunction (ArtworkId, ArtworkTypeId, TermId, VocabularyId)
SELECT
    @ArtworkId,
    @ArtworkTypeId,
    term.Id,
    term.VocabularyId
FROM
    @VocabularyTermIds AS termIds
    JOIN dbo.VocabularyTerms AS term ON term.Id = termIds.Id;

END;
