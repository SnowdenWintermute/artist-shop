CREATE OR ALTER PROCEDURE dbo.AddPainting @Name nvarchar(200),
@Slug nvarchar(200),
@Price decimal(10, 2),
@Stock int,
@DatePainted date,
@Description nvarchar(max),
@WidthCm decimal(6, 2),
@HeightCm decimal(6, 2),
-- how is it we are passing a "table type" as a parameter from
-- the frontend to this procedure?
@Images dbo.ShopItemImageList READONLY AS BEGIN
-- Stops SQL Server emitting a "(1 row affected)" message per statement. Those
-- are extra results the client has to skip past, and they confuse some drivers.
SET
NOCOUNT ON;

-- makes the "batch" abort if error and roll transaction back
SET
XACT_ABORT ON;

BEGIN TRANSACTION;

INSERT INTO
    dbo.ShopItems (Name, Slug, Price, Stock)
VALUES
    (@Name, @Slug, @Price, @Stock);

DECLARE @Id int;

-- it assigns @Id to the most recently created IDENTITY
-- in the current scope (batch, procedure, function or trigger)
SET
    @Id = SCOPE_IDENTITY();

INSERT INTO
    dbo.Paintings (Id, DatePainted, Description, WidthCm, HeightCm)
VALUES
    (
        @Id,
        @DatePainted,
        @Description,
        @WidthCm,
        @HeightCm
    );

-- inserts all the rows in the table returned from the select
INSERT INTO
    dbo.ShopItemImages (ShopItemId, Path, SortOrder, IsPrimary)
SELECT
    @Id,
    Path,
    SortOrder,
    IsPrimary
FROM
    @Images;

COMMIT TRANSACTION;

SELECT
    @Id;

END;
