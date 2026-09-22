-- Postgres has no table-valued parameters. A function takes an array instead: int[] for the id
-- lists, text[] for the names, and an array of one of these composite types where a row has
-- several columns. A composite type can't hold NOT NULL, a key or a UNIQUE, so the table each row
-- lands in is what rejects a bad one.
CREATE TYPE artwork_image_input AS (
    storage_key char(32),
    original_file_name varchar(260),
    sort_order int,
    is_primary boolean,
    width int,
    height int,
    blur_data_uri varchar(1000)
);

-- the products island's rows
CREATE TYPE product_input AS (
    product_type_id int,
    label varchar(100) COLLATE case_insensitive,
    price numeric(10, 2),
    edition_size int,
    stock int
);

CREATE TYPE series_name_and_slug AS (
    name varchar(256) COLLATE case_insensitive,
    slug varchar(200)
);
