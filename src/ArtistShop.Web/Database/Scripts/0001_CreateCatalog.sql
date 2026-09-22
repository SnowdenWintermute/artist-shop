-- Postgres compares text byte by byte unless a column says otherwise. These ICU collations are how
-- a column says otherwise. Non-deterministic means two different strings can count as equal, which
-- is the point, and Postgres won't allow one as a database's default, so each column names it.
-- level2 ignores case but not accents: 'Oil' collides with 'oil', 'cafe' and 'café' stay apart
CREATE COLLATION case_insensitive (provider = icu, locale = 'und-u-ks-level2', deterministic = false);

-- level1 ignores accents as well, so "cafe" finds "café". Only the title search uses it, with
-- COLLATE on the comparison, and LIKE over it needs Postgres 18
CREATE COLLATION case_and_accent_insensitive (
    provider = icu,
    locale = 'und-u-ks-level1',
    deterministic = false
);

CREATE TABLE artwork_types (
    -- ALWAYS refuses an id the insert supplies, as IDENTITY does without IDENTITY_INSERT
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_artwork_types PRIMARY KEY (id),
    name varchar(50) COLLATE case_insensitive NOT NULL,
    CONSTRAINT unique_artwork_types_name UNIQUE (name)
);

INSERT INTO
    artwork_types (name)
VALUES
    ('Painting'),
    ('Photograph'),
    ('Screenshot'),
    ('Sculpture');

-- defined by us; the ids must match the ArtworkField enum in C#
CREATE TABLE artwork_fields (
    id int,
    CONSTRAINT primary_key_artwork_fields PRIMARY KEY (id),
    name varchar(50) COLLATE case_insensitive NOT NULL,
    CONSTRAINT unique_artwork_fields_name UNIQUE (name),
    -- a field that only makes sense alongside another, like depth alongside height and width. A field
    -- with no requirement names itself: NOT NULL matters, because a foreign key with a NULL column
    -- isn't checked at all, which would let the junction below skip the requirement
    requires_artwork_field_id int NOT NULL,
    CONSTRAINT foreign_key_artwork_fields_requires_artwork_field FOREIGN KEY (requires_artwork_field_id) REFERENCES artwork_fields (id),
    -- lets the junction below copy the requirement under a foreign key
    CONSTRAINT unique_artwork_fields_id_requires UNIQUE (id, requires_artwork_field_id)
);

INSERT INTO
    artwork_fields (id, name, requires_artwork_field_id)
VALUES
    (1, 'Date created', 1),
    (2, 'Height and width', 2),
    (3, 'Depth', 2),
    (4, 'Duration', 4);

-- which fields the artist switched on for each type
CREATE TABLE artwork_type_and_artwork_fields_junction (
    artwork_type_id int NOT NULL,
    artwork_field_id int NOT NULL,
    -- copied from artwork_fields; the foreign key to artwork_fields keeps the copy honest
    requires_artwork_field_id int NOT NULL,
    CONSTRAINT primary_key_artwork_type_and_artwork_fields_junction PRIMARY KEY (artwork_type_id, artwork_field_id),
    CONSTRAINT foreign_key_artwork_type_fields_artwork_types FOREIGN KEY (artwork_type_id) REFERENCES artwork_types (id),
    CONSTRAINT foreign_key_artwork_type_fields_artwork_fields FOREIGN KEY (artwork_field_id, requires_artwork_field_id) REFERENCES artwork_fields (id, requires_artwork_field_id),
    -- points at a row of this same table: the type must also have the required field. A field that
    -- requires itself points at its own row, so it always passes
    CONSTRAINT foreign_key_artwork_type_fields_required_field FOREIGN KEY (artwork_type_id, requires_artwork_field_id) REFERENCES artwork_type_and_artwork_fields_junction (artwork_type_id, artwork_field_id)
);

-- a table value constructor: VALUES used as a table of rows, named like any other table
INSERT INTO
    artwork_type_and_artwork_fields_junction (artwork_type_id, artwork_field_id, requires_artwork_field_id)
SELECT
    artwork_type.id,
    artwork_field.id,
    artwork_field.requires_artwork_field_id
FROM
    (
        VALUES
            ('Painting', 1),
            ('Painting', 2),
            ('Photograph', 1),
            ('Photograph', 2),
            ('Screenshot', 1),
            ('Sculpture', 1),
            ('Sculpture', 2),
            ('Sculpture', 3)
    ) AS seed (artwork_type_name, artwork_field_id)
    JOIN artwork_types AS artwork_type ON artwork_type.name = seed.artwork_type_name
    JOIN artwork_fields AS artwork_field ON artwork_field.id = seed.artwork_field_id;

CREATE TABLE artworks (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_artworks PRIMARY KEY (id),
    artwork_type_id int NOT NULL,
    CONSTRAINT foreign_key_artworks_artwork_types FOREIGN KEY (artwork_type_id) REFERENCES artwork_types (id),
    CONSTRAINT unique_artworks_id_artwork_type UNIQUE (id, artwork_type_id),
    -- not unique, but bulk image matching finds an artwork by its name whatever the case
    name varchar(200) COLLATE case_insensitive NOT NULL,
    slug varchar(200) NOT NULL,
    CONSTRAINT unique_artworks_slug UNIQUE (slug),
    description text,
    date_created date,
    date_created_precision smallint,
    CONSTRAINT check_artworks_date_created CHECK (
        (
            date_created IS NULL
            AND date_created_precision IS NULL
        )
        OR (
            date_created IS NOT NULL
            AND date_created_precision IS NOT NULL
        )
    ),
    CONSTRAINT check_artworks_date_created_precision CHECK (date_created_precision IN (1, 2, 3)),
    -- the parts below the precision must be "the first": a year-only date is stored
    -- as January 1st, so two artworks from "2019" can't hold different hidden days
    CONSTRAINT check_artworks_date_created_unknown_parts CHECK (
        date_created_precision = 3
        OR (
            date_created_precision = 2
            AND EXTRACT(DAY FROM date_created) = 1
        )
        OR (
            date_created_precision = 1
            AND EXTRACT(MONTH FROM date_created) = 1
            AND EXTRACT(DAY FROM date_created) = 1
        )
    ),
    -- 4 digits before the decimal point and 4 after, so an inch value with
    -- 2 decimals converts to centimetres with no rounding
    -- in the order galleries list them: height x width x depth
    height_cm numeric(8, 4),
    width_cm numeric(8, 4),
    depth_cm numeric(8, 4),
    CONSTRAINT check_artworks_height_and_width CHECK (
        (
            height_cm IS NULL
            AND width_cm IS NULL
        )
        OR (
            height_cm IS NOT NULL
            AND width_cm IS NOT NULL
        )
    ),
    CONSTRAINT check_artworks_depth_needs_height_and_width CHECK (
        depth_cm IS NULL
        OR height_cm IS NOT NULL
    ),
    -- will pass if height/width null because x > 0 when x is null is UNKNOWN,
    -- and constraint only fail if evaluate to false
    CONSTRAINT check_artworks_height_cm CHECK (height_cm > 0),
    CONSTRAINT check_artworks_width_cm CHECK (width_cm > 0),
    CONSTRAINT check_artworks_depth_cm CHECK (depth_cm > 0),
    duration_seconds int,
    CONSTRAINT check_artworks_duration_seconds CHECK (duration_seconds > 0),
    -- now() is when the transaction began, so every artwork a CSV import adds would share one time
    -- and "recently added" couldn't order them. clock_timestamp() is the moment of the insert
    created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

-- bulk image matching looks artworks up by type and name
CREATE INDEX index_artworks_type_and_name ON artworks (artwork_type_id, name);

CREATE TABLE vocabularies (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_vocabularies PRIMARY KEY (id),
    name varchar(100) COLLATE case_insensitive NOT NULL,
    CONSTRAINT unique_vocabularies_name UNIQUE (name)
);

-- sets which vocabularies are allowed on which artwork types
CREATE TABLE vocabulary_and_artwork_types_junction (
    vocabulary_id int NOT NULL,
    artwork_type_id int NOT NULL,
    CONSTRAINT primary_key_vocabulary_and_artwork_types_junction PRIMARY KEY (vocabulary_id, artwork_type_id),
    CONSTRAINT foreign_key_vocabulary_artwork_types_vocabularies FOREIGN KEY (vocabulary_id) REFERENCES vocabularies (id),
    CONSTRAINT foreign_key_vocabulary_artwork_types_artwork_types FOREIGN KEY (artwork_type_id) REFERENCES artwork_types (id)
);

-- The enumerated words of a certain vocabulary, like if the vocabulary is "Support"
-- the terms could be "Paper", "Canvas" etc
CREATE TABLE vocabulary_terms (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_vocabulary_terms PRIMARY KEY (id),
    vocabulary_id int NOT NULL,
    CONSTRAINT foreign_key_vocabulary_terms_vocabularies FOREIGN KEY (vocabulary_id) REFERENCES vocabularies (id),
    name varchar(100) COLLATE case_insensitive NOT NULL,
    -- "Paper" can be both a "Support" and a "Medium", but not a support twice. UNIQUE rejects
    -- duplicates but the column's collation decides what a duplicate is, so 'Oil' collides with 'oil'
    CONSTRAINT unique_vocabulary_terms_vocabulary_name UNIQUE (vocabulary_id, name),
    CONSTRAINT unique_vocabulary_terms_id_vocabulary UNIQUE (id, vocabulary_id)
);

CREATE TABLE artwork_and_vocabulary_terms_junction (
    artwork_id int NOT NULL,
    artwork_type_id int NOT NULL,
    term_id int NOT NULL,
    vocabulary_id int NOT NULL,
    CONSTRAINT primary_key_artwork_and_vocabulary_terms_junction PRIMARY KEY (artwork_id, term_id),
    CONSTRAINT foreign_key_artwork_terms_artworks FOREIGN KEY (artwork_id, artwork_type_id) REFERENCES artworks (id, artwork_type_id) ON DELETE CASCADE,
    CONSTRAINT foreign_key_artwork_terms_vocabulary_terms FOREIGN KEY (term_id, vocabulary_id) REFERENCES vocabulary_terms (id, vocabulary_id),
    CONSTRAINT foreign_key_artwork_terms_vocabulary_artwork_types FOREIGN KEY (vocabulary_id, artwork_type_id) REFERENCES vocabulary_and_artwork_types_junction (vocabulary_id, artwork_type_id)
);

-- the primary key leads with artwork_id, so it can't find a term's rows: this serves term usage
-- counts, deleting a term, and the foreign key check when one is deleted
CREATE INDEX index_artwork_and_vocabulary_terms_junction_term ON artwork_and_vocabulary_terms_junction (term_id);

CREATE TABLE artwork_images (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_artwork_images PRIMARY KEY (id),
    artwork_id int NOT NULL,
    CONSTRAINT foreign_key_artwork_images_artworks FOREIGN KEY (artwork_id) REFERENCES artworks (id) ON DELETE CASCADE,
    -- always the 32 hexadecimal characters of a version 7 GUID
    storage_key char(32) NOT NULL,
    CONSTRAINT unique_artwork_images_storage_key UNIQUE (storage_key),
    original_file_name varchar(260),
    sort_order int NOT NULL,
    is_primary boolean NOT NULL DEFAULT false,
    width int NOT NULL,
    height int NOT NULL,
    blur_data_uri varchar(1000)
);

-- Postgres indexes the referenced side of a foreign key but not the referencing side, so without
-- this every image lookup, image count and cascade from a deleted artwork reads the whole table
CREATE INDEX index_artwork_images_artwork ON artwork_images (artwork_id);

-- a partial index: only the rows matching the WHERE are in it, so it forbids a second primary
-- image rather than a second of anything
CREATE UNIQUE INDEX unique_index_artwork_images_primary ON artwork_images (artwork_id)
WHERE
    is_primary;

CREATE TABLE series (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_series PRIMARY KEY (id),
    name varchar(256) COLLATE case_insensitive NOT NULL,
    CONSTRAINT unique_series_name UNIQUE (name),
    slug varchar(200) NOT NULL,
    CONSTRAINT unique_series_slug UNIQUE (slug),
    -- the artist's order, the default visitors see
    sort_order int NOT NULL,
    -- Postgres checks a plain UNIQUE after every row, so an UPDATE that swaps two positions would
    -- collide halfway. DEFERRABLE moves the check to the end of the statement
    CONSTRAINT unique_series_sort_order UNIQUE (sort_order) DEFERRABLE
);

-- any artwork type can join any series, mixed freely
CREATE TABLE artwork_and_series_junction (
    artwork_id int NOT NULL,
    series_id int NOT NULL,
    sort_order int NOT NULL,
    -- DEFERRABLE for the same reason as unique_series_sort_order
    CONSTRAINT unique_artwork_and_series_junction_series_sort_order UNIQUE (series_id, sort_order) DEFERRABLE,
    CONSTRAINT primary_key_artwork_and_series_junction PRIMARY KEY (artwork_id, series_id),
    CONSTRAINT foreign_key_artwork_series_artworks FOREIGN KEY (artwork_id) REFERENCES artworks (id) ON DELETE CASCADE,
    CONSTRAINT foreign_key_artwork_series_series FOREIGN KEY (series_id) REFERENCES series (id),
    -- the series cover is this artwork's primary image
    is_cover boolean NOT NULL DEFAULT false
);

CREATE UNIQUE INDEX unique_index_artwork_and_series_junction_cover ON artwork_and_series_junction (series_id)
WHERE
    is_cover;

CREATE TABLE product_types (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_product_types PRIMARY KEY (id),
    name varchar(50) COLLATE case_insensitive NOT NULL,
    CONSTRAINT unique_product_types_name UNIQUE (name),
    -- the one a form offers before the artist chooses. An id can't be written into the code, since
    -- these are rows the artist will manage, so the row says so itself
    is_default boolean NOT NULL DEFAULT false
);

-- partial, so it only forbids a second default rather than a second of anything
CREATE UNIQUE INDEX unique_index_product_types_default ON product_types (is_default)
WHERE
    is_default;

INSERT INTO
    product_types (name, is_default)
VALUES
    ('Original', true),
    ('Print', false),
    ('Postcard', false);

CREATE TABLE products (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_products PRIMARY KEY (id),
    artwork_id int NOT NULL,
    CONSTRAINT foreign_key_products_artworks FOREIGN KEY (artwork_id) REFERENCES artworks (id) ON DELETE CASCADE,
    product_type_id int NOT NULL,
    CONSTRAINT foreign_key_products_product_types FOREIGN KEY (product_type_id) REFERENCES product_types (id),
    -- tells two products of the same type apart, like "A4" and "A3"
    label varchar(100) COLLATE case_insensitive,
    -- Postgres lets any number of NULLs past a UNIQUE unless told otherwise, and two unlabelled
    -- prints of one artwork must clash
    CONSTRAINT unique_products_artwork_product_type_label UNIQUE NULLS NOT DISTINCT (artwork_id, product_type_id, label),
    price numeric(10, 2),
    CONSTRAINT check_products_price CHECK (price >= 0),
    -- how many were ever made; NULL means it can always be restocked
    edition_size int,
    CONSTRAINT check_products_edition_size CHECK (edition_size > 0),
    stock int NOT NULL,
    CONSTRAINT check_products_stock CHECK (stock >= 0),
    -- passes when edition_size is NULL, for the same reason as the dimension checks
    CONSTRAINT check_products_stock_within_edition CHECK (stock <= edition_size),
    -- only something with none left may have no price
    CONSTRAINT check_products_price_unless_sold_out CHECK (
        price IS NOT NULL
        OR stock = 0
    )
);
