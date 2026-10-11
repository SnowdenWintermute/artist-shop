DROP FUNCTION IF EXISTS remove_brand_image;

-- Returns the removed original's storage key, for the caller to delete, or NULL when there was none
CREATE FUNCTION remove_brand_image (p_type int) RETURNS text LANGUAGE sql AS $$
DELETE FROM brand_images
WHERE
    type = p_type
RETURNING
    storage_key;
$$;
