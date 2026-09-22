DROP FUNCTION IF EXISTS set_artwork_images;

-- The form posts the whole list every time, in the order the artist put it in, so replacing every
-- row is simpler than working out which moved. Nothing refers to an artwork_images row by its id,
-- so a kept image getting a new one costs nothing. On a new artwork the DELETE finds nothing.
CREATE FUNCTION set_artwork_images (p_artwork_id int, p_images artwork_image_input[]) RETURNS void LANGUAGE sql AS $$
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
        blur_data_uri
    )
SELECT
    p_artwork_id,
    image.storage_key,
    image.original_file_name,
    image.sort_order,
    image.is_primary,
    image.width,
    image.height,
    image.blur_data_uri
FROM
    unnest(p_images) AS image;
$$;
