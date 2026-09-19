-- the primary key uses the database's case-insensitive collation, so "Sunset" and "sunset" in one
-- list is an error: the caller folds them into one row and reports the files behind it

CREATE
TYPE dbo.ArtworkNameList AS
TABLE (Name nvarchar(200) NOT NULL, PRIMARY KEY (Name));
