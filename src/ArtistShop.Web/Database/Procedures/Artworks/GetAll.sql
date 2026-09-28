-- every artwork with everything get_artwork reads, one function per result set, for the site
-- export. Each child row carries its artwork's id so the repository can sort them onto their artwork
DROP FUNCTION IF EXISTS get_all_artworks;

CREATE FUNCTION get_all_artworks () RETURNS TABLE (
    id int,
    artwork_type_id int,
    artwork_type_name text,
    name text,
    slug text,
    description text,
    date_created date,
    date_created_precision smallint,
    height_cm numeric,
    width_cm numeric,
    depth_cm numeric,
    duration_seconds int
) LANGUAGE sql STABLE AS $$
SELECT
    artwork.id,
    artwork.artwork_type_id,
    artwork_type.name,
    artwork.name,
    artwork.slug,
    artwork.description,
    artwork.date_created,
    artwork.date_created_precision,
    artwork.height_cm,
    artwork.width_cm,
    artwork.depth_cm,
    artwork.duration_seconds
FROM
    artworks AS artwork
    JOIN artwork_types AS artwork_type ON artwork_type.id = artwork.artwork_type_id
ORDER BY
    artwork.id;
$$;

DROP FUNCTION IF EXISTS get_all_artwork_images;

CREATE FUNCTION get_all_artwork_images () RETURNS TABLE (
    artwork_id int,
    storage_key text,
    original_file_name text,
    is_primary boolean,
    width int,
    height int,
    blur_data_uri text
) LANGUAGE sql STABLE AS $$
SELECT
    image.artwork_id,
    image.storage_key,
    image.original_file_name,
    image.is_primary,
    image.width,
    image.height,
    image.blur_data_uri
FROM
    artwork_images AS image
ORDER BY
    image.artwork_id,
    image.sort_order;
$$;

DROP FUNCTION IF EXISTS get_all_artwork_series;

-- in the site's series order, so "an artwork's first series" means the same thing every time
CREATE FUNCTION get_all_artwork_series () RETURNS TABLE (artwork_id int, id int, name text, slug text) LANGUAGE sql STABLE AS $$
SELECT
    junction.artwork_id,
    series.id,
    series.name,
    series.slug
FROM
    series
    JOIN artwork_and_series_junction AS junction ON junction.series_id = series.id
ORDER BY
    junction.artwork_id,
    series.sort_order;
$$;

DROP FUNCTION IF EXISTS get_all_artwork_vocabulary_terms;

CREATE FUNCTION get_all_artwork_vocabulary_terms () RETURNS TABLE (
    artwork_id int,
    id int,
    name text,
    vocabulary_id int,
    vocabulary_name text
) LANGUAGE sql STABLE AS $$
SELECT
    junction.artwork_id,
    term.id,
    term.name,
    vocabulary.id,
    vocabulary.name
FROM
    artwork_and_vocabulary_terms_junction AS junction
    JOIN vocabulary_terms AS term ON term.id = junction.term_id
    JOIN vocabularies AS vocabulary ON vocabulary.id = junction.vocabulary_id
ORDER BY
    junction.artwork_id,
    term.name;
$$;

DROP FUNCTION IF EXISTS get_all_artwork_products;

CREATE FUNCTION get_all_artwork_products () RETURNS TABLE (
    artwork_id int,
    id int,
    product_type_id int,
    product_type_name text,
    product_type_is_default boolean,
    label text,
    price numeric,
    edition_size int,
    stock int
) LANGUAGE sql STABLE AS $$
SELECT
    product.artwork_id,
    product.id,
    product.product_type_id,
    product_type.name,
    product_type.is_default,
    product.label,
    product.price,
    product.edition_size,
    product.stock
FROM
    products AS product
    JOIN product_types AS product_type ON product_type.id = product.product_type_id
ORDER BY
    product.artwork_id,
    product.id;
$$;
