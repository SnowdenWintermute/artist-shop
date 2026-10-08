DROP FUNCTION IF EXISTS get_collection;

CREATE FUNCTION get_collection (p_id int) RETURNS TABLE (id int, name text, slug text) LANGUAGE sql STABLE AS $$
SELECT
    collections.id,
    collections.name,
    collections.slug
FROM
    collections
WHERE
    collections.id = p_id;
$$;

DROP FUNCTION IF EXISTS get_collection_works;

CREATE FUNCTION get_collection_works (p_id int) RETURNS TABLE (
    id int,
    name text,
    work_type_name text,
    is_cover boolean,
    storage_key text,
    original_file_name text,
    width int,
    height int,
    blur_data_uri text
) LANGUAGE sql STABLE AS $$
SELECT
    work.id,
    work.name,
    work_type.name,
    junction.is_cover,
    primary_image.storage_key,
    primary_image.original_file_name,
    primary_image.width,
    primary_image.height,
    primary_image.blur_data_uri
FROM
    work_and_collection_junction AS junction
    JOIN works AS work ON work.id = junction.work_id
    JOIN work_types AS work_type ON work_type.id = work.work_type_id
    LEFT JOIN work_images AS primary_image ON primary_image.work_id = work.id
    AND primary_image.is_primary
WHERE
    junction.collection_id = p_id
ORDER BY
    junction.sort_order;
$$;
