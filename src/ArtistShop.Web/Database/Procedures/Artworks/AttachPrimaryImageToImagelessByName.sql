DROP FUNCTION IF EXISTS attach_primary_image_to_imageless_artwork_by_name;

-- One row: what was found, and every artwork matched so the report can link to them. The match
-- types follow get_artwork_name_match_types, and one imageless artwork here means the image was
-- attached to it
CREATE FUNCTION attach_primary_image_to_imageless_artwork_by_name (
    p_artwork_type_id int,
    p_artwork_name text,
    p_storage_key text,
    p_original_file_name text,
    p_width int,
    p_height int,
    p_blur_data_uri text
) RETURNS TABLE (match_type smallint, artwork_ids int[]) LANGUAGE plpgsql AS $$
DECLARE
    one_imageless_artwork CONSTANT smallint := 1;
    no_artwork CONSTANT smallint := 2;
    several_artworks CONSTANT smallint := 3;
    artwork_with_images CONSTANT smallint := 4;
    matching_artwork_ids int[];
    found_match_type smallint;
BEGIN
    IF NOT EXISTS (
        SELECT
        FROM
            artwork_types
        WHERE
            artwork_types.id = p_artwork_type_id
    ) THEN
        RAISE EXCEPTION 'The artwork type no longer exists.' USING ERRCODE = 'SH010';
    END IF;

    -- The column's case_insensitive collation makes = ignore case, so "sunset" matches "Sunset".
    -- FOR UPDATE holds the matched rows until the transaction ends: a second upload for the same
    -- artwork waits here, then sees the image this one inserted
    matching_artwork_ids := ARRAY(
        SELECT
            artwork.id
        FROM
            artworks AS artwork
        WHERE
            artwork.artwork_type_id = p_artwork_type_id
            AND artwork.name = p_artwork_name
        FOR UPDATE
    );

    IF cardinality(matching_artwork_ids) = 0 THEN
        found_match_type := no_artwork;
    ELSIF cardinality(matching_artwork_ids) > 1 THEN
        found_match_type := several_artworks;
    ELSIF EXISTS (
        SELECT
        FROM
            artwork_images AS image
        WHERE
            image.artwork_id = matching_artwork_ids[1]
    ) THEN
        found_match_type := artwork_with_images;
    ELSE
        -- arrays count from 1
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
        VALUES
            (
                matching_artwork_ids[1],
                p_storage_key,
                p_original_file_name,
                0,
                true,
                p_width,
                p_height,
                p_blur_data_uri
            );

        found_match_type := one_imageless_artwork;
    END IF;

    RETURN QUERY
    SELECT
        found_match_type,
        matching_artwork_ids;
END;
$$;
