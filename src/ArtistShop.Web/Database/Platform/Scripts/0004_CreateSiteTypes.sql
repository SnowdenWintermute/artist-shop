-- What every site's schema uses and none owns: its collations, and the shapes of its functions'
-- array inputs. Made once here, since a copy in each site_<id> would make their names ambiguous to
-- Npgsql, which looks types up across the whole database. A site's migrations and connections put
-- this schema second in search_path, after the site's own (SiteDatabases)
CREATE SCHEMA site_types;

-- Postgres compares text byte by byte unless a column says otherwise. These ICU collations are how
-- a column says otherwise. Non-deterministic means two different strings can count as equal, which
-- is the point, and Postgres won't allow one as a database's default, so each column names it.
-- level2 ignores case but not accents: 'Oil' collides with 'oil', 'cafe' and 'café' stay apart
CREATE COLLATION site_types.case_insensitive (provider = icu, locale = 'und-u-ks-level2', deterministic = false);

-- level1 ignores accents as well, so "cafe" finds "café". Only the title search uses it, with
-- COLLATE on the comparison, and LIKE over it needs Postgres 18
CREATE COLLATION site_types.case_and_accent_insensitive (
    provider = icu,
    locale = 'und-u-ks-level1',
    deterministic = false
);

-- Postgres has no table-valued parameters. A function takes an array instead: int[] for the id
-- lists, text[] for the names, and an array of one of these composite types where a row has
-- several columns. A composite type can't hold NOT NULL, a key or a UNIQUE, so the table each row
-- lands in is what rejects a bad one.
CREATE TYPE site_types.artwork_image_input AS (
    storage_key char(32),
    original_file_name varchar(260),
    sort_order int,
    is_primary boolean,
    width int,
    height int,
    blur_data_uri varchar(1000)
);

-- the products island's rows
CREATE TYPE site_types.product_input AS (
    product_type_id int,
    label varchar(100) COLLATE site_types.case_insensitive,
    price numeric(10, 2),
    edition_size int,
    stock int
);

CREATE TYPE site_types.series_name_and_slug AS (
    name varchar(256) COLLATE site_types.case_insensitive,
    slug varchar(200)
);
