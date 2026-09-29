DROP FUNCTION IF EXISTS append_artwork_image;

-- After the artwork's other images, and its primary image when it has none. The artwork's row is
-- locked, so two uploads for one artwork take turns and each sees the other's image
CREATE FUNCTION append_artwork_image (
    p_artwork_id int,
    p_storage_key text,
    p_original_file_name text,
    p_width int,
    p_height int,
    p_blur_data_uri text
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    PERFORM
    FROM
        artworks
    WHERE
        artworks.id = p_artwork_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The artwork no longer exists.' USING ERRCODE = 'SH003';
    END IF;

    -- with no images, max is null and count is 0, so it's first and primary
    INSERT INTO
        artwork_images (
            artwork_id,
            storage_key,
            original_file_name,
            sort_order,
            is_primary,
            width,
            height,
            blur_data_uri
        )
    SELECT
        p_artwork_id,
        p_storage_key,
        p_original_file_name,
        COALESCE(max(image.sort_order) + 1, 0),
        count(*) = 0,
        p_width,
        p_height,
        p_blur_data_uri
    FROM
        artwork_images AS image
    WHERE
        image.artwork_id = p_artwork_id;
END;
$$;
