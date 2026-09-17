-- the primary key uses the database's case-insensitive collation, so "Sunset" and "sunset" in one
-- list is an error: the caller reports such duplicates instead of sending them
CREATE
TYPE dbo.ArtworkNameList AS
TABLE (Name nvarchar(200) NOT NULL, PRIMARY KEY (Name));
