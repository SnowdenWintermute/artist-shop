-- rows in a table-valued parameter have no guaranteed order, so the order travels as a column
CREATE
TYPE dbo.OrderedIdList AS
TABLE (
    Id int NOT NULL,
    SortOrder int NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (SortOrder)
);
