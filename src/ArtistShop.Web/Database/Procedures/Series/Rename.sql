CREATE OR ALTER PROCEDURE dbo.RenameSeries @Id int,
@Name nvarchar(256),
@Slug nvarchar(200) AS BEGIN
SET
NOCOUNT ON;

UPDATE dbo.Series
SET
    Name = @Name,
    Slug = @Slug
WHERE
    Id = @Id;

IF @@ROWCOUNT = 0 THROW 50004,
'The series no longer exists.',
1;

END;
