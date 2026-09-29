DROP FUNCTION IF EXISTS get_artwork_image_file_names;

-- the name each image was uploaded under, so the bulk upload can leave out a file an artwork
-- already has an image from. An image saved without one isn't listed
CREATE FUNCTION get_artwork_image_file_names (p_artwork_ids int[]) RETURNS TABLE (artwork_id int, original_file_name text) LANGUAGE sql STABLE AS $$
SELECT
    image.artwork_id,
    image.original_file_name::text
FROM
    artwork_images AS image
WHERE
    image.artwork_id = ANY (p_artwork_ids)
    AND image.original_file_name IS NOT NULL;
$$;
