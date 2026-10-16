DROP FUNCTION IF EXISTS get_uploaded_image_page;

-- One page of the Images page's list, newest first, each image with how many posts and text components
-- show it. A search matches alt text or file name, ignoring case and accents, its LIKE wildcards
-- escaped as find_work_ids_by_name escapes them. total_count is every match's, repeated on each row
CREATE FUNCTION get_uploaded_image_page (p_search text, p_offset int, p_page_size int) RETURNS TABLE (
    id int,
    storage_key text,
    original_file_name text,
    width int,
    height int,
    blur_data_uri text,
    alt_text text,
    uploaded_at timestamptz,
    used_in_count int,
    total_count int
) LANGUAGE plpgsql STABLE AS $$
DECLARE
    pattern text := '%' || replace(replace(replace(p_search, '\', '\\'), '%', '\%'), '_', '\_') || '%';
BEGIN
    RETURN QUERY
    SELECT
        image.id,
        image.storage_key::text,
        image.original_file_name::text,
        image.width,
        image.height,
        image.blur_data_uri::text,
        image.alt_text::text,
        image.uploaded_at,
        (
            (
                SELECT
                    count(*)
                FROM
                    post_and_uploaded_images_junction AS post_image
                WHERE
                    post_image.uploaded_image_id = image.id
            ) + (
                SELECT
                    count(*)
                FROM
                    custom_component_and_uploaded_images_junction AS component_image
                WHERE
                    component_image.uploaded_image_id = image.id
            )
        )::int,
        (COUNT(*) OVER ())::int
    FROM
        uploaded_images AS image
    WHERE
        p_search IS NULL
        OR image.alt_text COLLATE case_and_accent_insensitive LIKE pattern
        OR image.original_file_name COLLATE case_and_accent_insensitive LIKE pattern
    ORDER BY
        image.uploaded_at DESC,
        image.id DESC
    OFFSET
        p_offset
    LIMIT
        p_page_size;
END;
$$;
