-- one function per result set; the repository runs all five in one command
DROP FUNCTION IF EXISTS get_artwork;

CREATE FUNCTION get_artwork (p_id int) RETURNS TABLE (
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
WHERE
    artwork.id = p_id;
$$;

DROP FUNCTION IF EXISTS get_artwork_images;

CREATE FUNCTION get_artwork_images (p_artwork_id int) RETURNS TABLE (
    storage_key text,
    original_file_name text,
    is_primary boolean,
    width int,
    height int,
    blur_data_uri text
) LANGUAGE sql STABLE AS $$
SELECT
    image.storage_key,
    image.original_file_name,
    image.is_primary,
    image.width,
    image.height,
    image.blur_data_uri
FROM
    artwork_images AS image
WHERE
    image.artwork_id = p_artwork_id
ORDER BY
    image.sort_order;
$$;

DROP FUNCTION IF EXISTS get_artwork_series;

CREATE FUNCTION get_artwork_series (p_artwork_id int) RETURNS TABLE (id int, name text, slug text) LANGUAGE sql STABLE AS $$
SELECT
    series.id,
    series.name,
    series.slug
FROM
    series
    JOIN artwork_and_series_junction AS junction ON junction.series_id = series.id
WHERE
    junction.artwork_id = p_artwork_id;
$$;

DROP FUNCTION IF EXISTS get_artwork_vocabulary_terms;

-- each term with its vocabulary, so the page can show "Medium: Acrylic" without another query
CREATE FUNCTION get_artwork_vocabulary_terms (p_artwork_id int) RETURNS TABLE (
    id int,
    name text,
    vocabulary_id int,
    vocabulary_name text
) LANGUAGE sql STABLE AS $$
SELECT
    term.id,
    term.name,
    vocabulary.id,
    vocabulary.name
FROM
    artwork_and_vocabulary_terms_junction AS junction
    JOIN vocabulary_terms AS term ON term.id = junction.term_id
    JOIN vocabularies AS vocabulary ON vocabulary.id = junction.vocabulary_id
WHERE
    junction.artwork_id = p_artwork_id;
$$;

DROP FUNCTION IF EXISTS get_artwork_products;

CREATE FUNCTION get_artwork_products (p_artwork_id int) RETURNS TABLE (
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
WHERE
    product.artwork_id = p_artwork_id
ORDER BY
    product.id;
$$;
