DROP FUNCTION IF EXISTS get_artwork_image_sha256s;

-- the hashes stored for these images; an image without one isn't listed
CREATE FUNCTION get_artwork_image_sha256s (p_storage_keys text[]) RETURNS TABLE (storage_key text, sha256 text) LANGUAGE sql STABLE AS $$
SELECT
    image.storage_key,
    image.sha256
FROM
    artwork_images AS image
WHERE
    image.storage_key = ANY (p_storage_keys)
    AND image.sha256 IS NOT NULL;
$$;
