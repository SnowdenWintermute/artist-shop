DROP FUNCTION IF EXISTS get_work_images_by_storage_keys;

-- The images a post's embeds name, each with the work it belongs to, in one query however many
-- embeds the post has. A key that matches nothing, because its image was deleted, has no row.
-- image_number is where the image sits on the work page, counted from one as its address does
CREATE FUNCTION get_work_images_by_storage_keys (p_storage_keys text[]) RETURNS TABLE (
    work_id int,
    work_name text,
    work_slug text,
    storage_key text,
    original_file_name text,
    width int,
    height int,
    blur_data_uri text,
    image_number int
) LANGUAGE sql STABLE AS $$
SELECT
    work.id,
    work.name,
    work.slug,
    image.storage_key,
    image.original_file_name,
    image.width,
    image.height,
    image.blur_data_uri,
    (
        SELECT
            count(*)::int
        FROM
            work_images AS earlier
        WHERE
            earlier.work_id = image.work_id
            AND earlier.sort_order <= image.sort_order
    )
FROM
    work_images AS image
    JOIN works AS work ON work.id = image.work_id
WHERE
    -- cast to the column's own type: compared as text, the unique index on it couldn't be used
    image.storage_key = ANY (p_storage_keys::char(32)[]);
$$;
