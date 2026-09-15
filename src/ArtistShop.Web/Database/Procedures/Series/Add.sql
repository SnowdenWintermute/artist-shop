CREATE OR ALTER PROCEDURE dbo.AddSeries @Name nvarchar(256),
@Slug nvarchar(200) AS BEGIN
SET
NOCOUNT ON;

-- a slug another series has fails Unique_Series_Slug: unlike paintings, series slugs aren't numbered
INSERT INTO
    dbo.Series (Name, Slug)
    OUTPUT INSERTED.Id
VALUES
    (@Name, @Slug);

END;
