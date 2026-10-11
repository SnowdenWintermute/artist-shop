DROP FUNCTION IF EXISTS get_brand_image_file;

-- two files with the same bytes, as when one upload is both logos, are interchangeable
CREATE FUNCTION get_brand_image_file (p_sha256 text) RETURNS TABLE (format int, bytes bytea) LANGUAGE sql STABLE AS $$
SELECT
    file.format,
    file.bytes
FROM
    brand_image_files AS file
WHERE
    file.sha256 = p_sha256
LIMIT
    1;
$$;
