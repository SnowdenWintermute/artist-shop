DROP FUNCTION IF EXISTS get_artwork_images_by_storage_keys;

-- The images a post's embeds name, each with the artwork it belongs to, in one query however many
-- embeds the post has. A key that matches nothing, because its image was deleted, has no row.
-- image_number is where the image sits on the artwork page, counted from one as its address does
CREATE FUNCTION get_artwork_images_by_storage_keys (p_storage_keys text[]) RETURNS TABLE (
    artwork_id int,
    artwork_name text,
    artwork_slug text,
    storage_key text,
    original_file_name text,
    width int,
    height int,
    blur_data_uri text,
    image_number int
) LANGUAGE sql STABLE AS $$
SELECT
    artwork.id,
    artwork.name,
    artwork.slug,
    image.storage_key,
    image.original_file_name,
    image.width,
    image.height,
    image.blur_data_uri,
    (
        SELECT
            count(*)::int
        FROM
            artwork_images AS earlier
        WHERE
            earlier.artwork_id = image.artwork_id
            AND earlier.sort_order <= image.sort_order
    )
FROM
    artwork_images AS image
    JOIN artworks AS artwork ON artwork.id = image.artwork_id
WHERE
    -- cast to the column's own type: compared as text, the unique index on it couldn't be used
    image.storage_key = ANY (p_storage_keys::char(32)[]);
$$;
