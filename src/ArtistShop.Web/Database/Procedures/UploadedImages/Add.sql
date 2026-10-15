DROP FUNCTION IF EXISTS add_uploaded_image;

CREATE FUNCTION add_uploaded_image (
    p_storage_key text,
    p_original_file_name text,
    p_sha256 text,
    p_width int,
    p_height int,
    p_blur_data_uri text,
    p_alt_text text
) RETURNS int LANGUAGE sql AS $$
INSERT INTO
    uploaded_images (storage_key, original_file_name, sha256, width, height, blur_data_uri, alt_text)
VALUES
    (p_storage_key, p_original_file_name, p_sha256, p_width, p_height, p_blur_data_uri, p_alt_text)
RETURNING
    id;
$$;
