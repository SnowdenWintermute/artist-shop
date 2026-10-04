DROP FUNCTION IF EXISTS update_artwork_images;

-- The images only, for the artwork table's images dialog, whose rows save their other fields on their
-- own. The lock makes an upload appending to this artwork wait, as update_artwork's does
CREATE FUNCTION update_artwork_images (p_id int, p_images artwork_image_input[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    PERFORM
    FROM
        artworks AS artwork
    WHERE
        artwork.id = p_id
    FOR NO KEY UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The artwork no longer exists.' USING ERRCODE = 'SH003';
    END IF;

    PERFORM set_artwork_images(p_id, p_images);
END;
$$;
