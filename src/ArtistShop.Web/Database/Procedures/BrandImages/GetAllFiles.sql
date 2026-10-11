DROP FUNCTION IF EXISTS get_all_brand_image_files;

-- everything but the bytes, which only get_brand_image_file reads
CREATE FUNCTION get_all_brand_image_files () RETURNS TABLE (
    image_type int,
    original_file_name text,
    type int,
    format int,
    width int,
    height int,
    sha256 text
) LANGUAGE sql STABLE AS $$
SELECT
    image.type,
    image.original_file_name,
    file.type,
    file.format,
    file.width,
    file.height,
    file.sha256
FROM
    brand_images AS image
    JOIN brand_image_files AS file ON file.image_type = image.type;
$$;
