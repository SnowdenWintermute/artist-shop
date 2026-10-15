DROP FUNCTION IF EXISTS get_uploaded_images_by_storage_keys;

-- The library images a text's embeds name, in one query however many there are. A key that matches
-- nothing, because its image was deleted, has no row
CREATE FUNCTION get_uploaded_images_by_storage_keys (p_storage_keys text[]) RETURNS TABLE (
    id int,
    storage_key text,
    original_file_name text,
    width int,
    height int,
    blur_data_uri text,
    alt_text text,
    uploaded_at timestamptz
) LANGUAGE sql STABLE AS $$
SELECT
    image.id,
    image.storage_key,
    image.original_file_name,
    image.width,
    image.height,
    image.blur_data_uri,
    image.alt_text,
    image.uploaded_at
FROM
    uploaded_images AS image
WHERE
    image.storage_key = ANY (p_storage_keys);
$$;
