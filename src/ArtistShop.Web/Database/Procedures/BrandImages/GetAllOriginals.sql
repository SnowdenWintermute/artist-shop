DROP FUNCTION IF EXISTS get_all_brand_image_originals;

-- each image's original on disk, for the export
CREATE FUNCTION get_all_brand_image_originals () RETURNS TABLE (
    type int,
    storage_key text,
    original_file_name text,
    sha256 text
) LANGUAGE sql STABLE AS $$
SELECT
    image.type,
    image.storage_key,
    image.original_file_name,
    image.sha256
FROM
    brand_images AS image;
$$;
