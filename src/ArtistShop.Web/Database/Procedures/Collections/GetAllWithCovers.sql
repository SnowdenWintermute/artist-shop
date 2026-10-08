DROP FUNCTION IF EXISTS get_collections_with_covers;

CREATE FUNCTION get_collections_with_covers (p_only_works_with_images boolean) RETURNS TABLE (
    id int,
    name text,
    slug text,
    work_count int,
    cover_storage_key text,
    cover_original_file_name text,
    cover_width int,
    cover_height int,
    cover_blur_data_uri text
) LANGUAGE sql STABLE AS $$
SELECT
    collections.id,
    collections.name,
    collections.slug,
    (
        SELECT
            COUNT(*)::int
        FROM
            work_and_collection_junction AS junction
        WHERE
            junction.collection_id = collections.id
            AND (
                NOT p_only_works_with_images
                OR EXISTS (
                    SELECT
                    FROM
                        work_images AS image
                    WHERE
                        image.work_id = junction.work_id
                )
            )
    ),
    cover.storage_key,
    cover.original_file_name,
    cover.width,
    cover.height,
    cover.blur_data_uri
FROM
    collections
    -- LATERAL lets the subquery use the collection from its own row, so it runs once per collection the
    -- way OUTER APPLY does. LEFT JOIN ... ON true keeps a collection with no match
    LEFT JOIN LATERAL (
        SELECT
            primary_image.storage_key,
            primary_image.original_file_name,
            primary_image.width,
            primary_image.height,
            primary_image.blur_data_uri
        FROM
            work_and_collection_junction AS junction
            -- an inner join, so works with no images can't become the cover
            JOIN work_images AS primary_image ON primary_image.work_id = junction.work_id
            AND primary_image.is_primary
        WHERE
            junction.collection_id = collections.id
        ORDER BY
            -- true sorts after false, so DESC puts the starred cover first
            junction.is_cover DESC,
            junction.sort_order
        LIMIT
            1
    ) AS cover ON true
WHERE
    -- a collection whose works have no photographs yet has nothing a visitor could look at
    NOT p_only_works_with_images
    OR cover.storage_key IS NOT NULL
ORDER BY
    collections.sort_order;
$$;
