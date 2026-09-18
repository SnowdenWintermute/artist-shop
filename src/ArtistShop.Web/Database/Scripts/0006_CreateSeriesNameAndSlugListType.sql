-- the primary key uses the database's case-insensitive collation, so "Seascapes" and "seascapes"
-- in one list is an error: the import folds them into one name before sending
CREATE
TYPE dbo.SeriesNameAndSlugList AS
TABLE (
    Name nvarchar(256) NOT NULL,
    Slug nvarchar(200) NOT NULL,
    PRIMARY KEY (Name)
);
