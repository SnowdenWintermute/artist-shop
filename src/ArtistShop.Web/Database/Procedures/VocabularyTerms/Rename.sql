CREATE OR ALTER PROCEDURE dbo.RenameVocabularyTerm @Id int,
@Name nvarchar(100) AS BEGIN
SET
NOCOUNT ON;

UPDATE dbo.VocabularyTerms
SET
    Name = @Name
WHERE
    Id = @Id;

IF @@ROWCOUNT = 0 THROW 50001,
'The vocabulary term no longer exists.',
1;

END;
