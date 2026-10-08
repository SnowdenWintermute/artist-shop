-- works with everything get_work reads, one function per result set: every work for the
-- site export when p_work_ids is null, or those ids for the work table. Each child row carries
-- its work's id so the repository can sort them onto their work
DROP FUNCTION IF EXISTS get_all_works;

CREATE FUNCTION get_all_works (p_work_ids int[]) RETURNS TABLE (
    id int,
    work_type_id int,
    work_type_name text,
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
    work.id,
    work.work_type_id,
    work_type.name,
    work.name,
    work.slug,
    work.description,
    work.date_created,
    work.date_created_precision,
    work.height_cm,
    work.width_cm,
    work.depth_cm,
    work.duration_seconds
FROM
    works AS work
    JOIN work_types AS work_type ON work_type.id = work.work_type_id
WHERE
    p_work_ids IS NULL
    OR work.id = ANY (p_work_ids)
ORDER BY
    work.id;
$$;

DROP FUNCTION IF EXISTS get_all_work_images;

CREATE FUNCTION get_all_work_images (p_work_ids int[]) RETURNS TABLE (
    work_id int,
    storage_key text,
    original_file_name text,
    is_primary boolean,
    width int,
    height int,
    blur_data_uri text
) LANGUAGE sql STABLE AS $$
SELECT
    image.work_id,
    image.storage_key,
    image.original_file_name,
    image.is_primary,
    image.width,
    image.height,
    image.blur_data_uri
FROM
    work_images AS image
WHERE
    p_work_ids IS NULL
    OR image.work_id = ANY (p_work_ids)
ORDER BY
    image.work_id,
    image.sort_order;
$$;

DROP FUNCTION IF EXISTS get_all_work_collections;

-- in the site's collection order, so "a work's first collection" means the same thing every time
CREATE FUNCTION get_all_work_collections (p_work_ids int[]) RETURNS TABLE (work_id int, id int, name text, slug text) LANGUAGE sql STABLE AS $$
SELECT
    junction.work_id,
    collections.id,
    collections.name,
    collections.slug
FROM
    collections
    JOIN work_and_collection_junction AS junction ON junction.collection_id = collections.id
WHERE
    p_work_ids IS NULL
    OR junction.work_id = ANY (p_work_ids)
ORDER BY
    junction.work_id,
    collections.sort_order;
$$;

DROP FUNCTION IF EXISTS get_all_work_vocabulary_terms;

CREATE FUNCTION get_all_work_vocabulary_terms (p_work_ids int[]) RETURNS TABLE (
    work_id int,
    id int,
    name text,
    vocabulary_id int,
    vocabulary_name text
) LANGUAGE sql STABLE AS $$
SELECT
    junction.work_id,
    term.id,
    term.name,
    vocabulary.id,
    vocabulary.name
FROM
    work_and_vocabulary_terms_junction AS junction
    JOIN vocabulary_terms AS term ON term.id = junction.term_id
    JOIN vocabularies AS vocabulary ON vocabulary.id = junction.vocabulary_id
WHERE
    p_work_ids IS NULL
    OR junction.work_id = ANY (p_work_ids)
ORDER BY
    junction.work_id,
    term.name;
$$;

DROP FUNCTION IF EXISTS get_all_work_products;

CREATE FUNCTION get_all_work_products (p_work_ids int[]) RETURNS TABLE (
    work_id int,
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
    product.work_id,
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
    p_work_ids IS NULL
    OR product.work_id = ANY (p_work_ids)
ORDER BY
    product.work_id,
    product.id;
$$;
