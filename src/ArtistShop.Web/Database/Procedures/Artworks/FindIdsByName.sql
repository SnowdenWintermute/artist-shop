CREATE OR ALTER PROCEDURE dbo.FindArtworkIdsByName @Search nvarchar(200) AS BEGIN
SET
NOCOUNT ON;

-- LIKE's own wildcards are hidden by wrapping each one in brackets, so a title holding a
-- % is searched for literally. The [ is replaced first, or it would break the brackets
-- the other two replacements add
DECLARE @Pattern nvarchar(700) = N'%' + REPLACE(
    REPLACE(REPLACE(@Search, N'[', N'[[]'), N'%', N'[%]'),
    N'_',
    N'[_]'
) + N'%';

SELECT
    Id
FROM
    dbo.Artworks
WHERE
    -- AI is accent-insensitive, so "cafe" finds "café". The column's own collation is
    -- accent-sensitive, which is right for term names but wrong for a search box
    Name COLLATE Latin1_General_100_CI_AI LIKE @Pattern;

END;
