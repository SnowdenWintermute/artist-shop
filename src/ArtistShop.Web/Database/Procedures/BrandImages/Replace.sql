DROP FUNCTION IF EXISTS replace_brand_image;

-- Returns the storage key of the original it replaced, for the caller to delete, or NULL when the
-- website had none of this type
CREATE FUNCTION replace_brand_image (
    p_type int,
    p_storage_key text,
    p_original_file_name text,
    p_sha256 text,
    p_files brand_image_file_input[]
) RETURNS text LANGUAGE plpgsql AS $$
DECLARE
    replaced_storage_key text;
BEGIN
    -- the files go with it, by the foreign key's cascade
    DELETE FROM brand_images
    WHERE
        type = p_type
    RETURNING
        storage_key INTO replaced_storage_key;

    INSERT INTO
        brand_images (type, storage_key, original_file_name, sha256)
    VALUES
        (p_type, p_storage_key, p_original_file_name, p_sha256);

    INSERT INTO
        brand_image_files (image_type, type, format, width, height, sha256, bytes)
    SELECT
        p_type,
        file.type,
        file.format,
        file.width,
        file.height,
        file.sha256,
        file.bytes
    FROM
        unnest(p_files) AS file;

    RETURN replaced_storage_key;
END;
$$;
