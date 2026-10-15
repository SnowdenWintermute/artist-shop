DROP FUNCTION IF EXISTS set_uploaded_image_alt_text;

-- false when no image has this key, as when it was deleted after the page was sent
CREATE FUNCTION set_uploaded_image_alt_text (p_storage_key text, p_alt_text text) RETURNS boolean LANGUAGE plpgsql AS $$
BEGIN
    UPDATE uploaded_images
    SET
        alt_text = p_alt_text
    WHERE
        storage_key = p_storage_key;

    RETURN FOUND;
END;
$$;
