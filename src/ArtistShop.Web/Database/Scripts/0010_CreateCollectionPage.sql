-- The settings of the fixed page each collection has: one row per website, as wording. The page
-- draws its works with a card grid, of which only these are the artist's to choose
CREATE TABLE collection_page (
    id boolean NOT NULL DEFAULT true,
    CONSTRAINT primary_key_collection_page PRIMARY KEY (id),
    CONSTRAINT check_collection_page_one_row CHECK (id),
    -- null until the artist saves, for CollectionPageSettings.Default's, so the defaults live only in C#.
    -- layout is CardGridLayout's id
    layout int,
    cards_per_page int,
    CONSTRAINT check_collection_page_cards_per_page_positive CHECK (cards_per_page > 0)
);

INSERT INTO collection_page DEFAULT VALUES;
