DROP FUNCTION IF EXISTS set_artwork_images;

-- The form posts the whole list every time, in the order the artist put it in, so replacing every
-- row is simpler than working out which moved. Nothing refers to an artwork_images row by its id,
-- so a kept image getting a new one costs nothing. On a new artwork the DELETE finds nothing. The
-- form doesn't send hashes, since only the server sets them, so a kept image keeps its own
CREATE FUNCTION set_artwork_images (p_artwork_id int, p_images artwork_image_input[]) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    -- storage key -> hash; ->> reads a key's value as text, and a key that isn't there gives NULL
    kept_hashes jsonb;
BEGIN
    SELECT
        COALESCE(jsonb_object_agg(image.storage_key::text, image.sha256), '{}')
    INTO kept_hashes
    FROM
        artwork_images AS image
    WHERE
        image.artwork_id = p_artwork_id
        AND image.sha256 IS NOT NULL;

    DELETE FROM artwork_images
    WHERE
        artwork_id = p_artwork_id;

    -- a removed image's file is left behind with no row pointing at it, which is what
    -- OrphanedImageSweeper looks for
    INSERT INTO
        artwork_images (
            artwork_id,
            storage_key,
            original_file_name,
            sort_order,
            is_primary,
            width,
            height,
            blur_data_uri,
            sha256
        )
    SELECT
        p_artwork_id,
        image.storage_key,
        image.original_file_name,
        image.sort_order,
        image.is_primary,
        image.width,
        image.height,
        image.blur_data_uri,
        kept_hashes ->> image.storage_key::text
    FROM
        unnest(p_images) AS image;
END;
$$;
