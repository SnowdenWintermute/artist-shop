DROP FUNCTION IF EXISTS attach_primary_image_to_imageless_work_by_name;

-- One row: what was found, and every work matched so the report can link to them. The match
-- types follow get_work_name_match_types, and one imageless work here means the image was
-- attached to it
CREATE FUNCTION attach_primary_image_to_imageless_work_by_name (
    p_work_type_id int,
    p_work_name text,
    p_storage_key text,
    p_original_file_name text,
    p_width int,
    p_height int,
    p_blur_data_uri text,
    p_sha256 text
) RETURNS TABLE (match_type smallint, work_ids int[]) LANGUAGE plpgsql AS $$
DECLARE
    one_imageless_work CONSTANT smallint := 1;
    no_work CONSTANT smallint := 2;
    several_works CONSTANT smallint := 3;
    work_with_images CONSTANT smallint := 4;
    matching_work_ids int[];
    found_match_type smallint;
BEGIN
    IF NOT EXISTS (
        SELECT
        FROM
            work_types
        WHERE
            work_types.id = p_work_type_id
    ) THEN
        RAISE EXCEPTION 'The work type no longer exists.' USING ERRCODE = 'SH010';
    END IF;

    -- The column's case_insensitive collation makes = ignore case, so "sunset" matches "Sunset".
    -- FOR UPDATE holds the matched rows until the transaction ends: a second upload for the same
    -- work waits here, then sees the image this one inserted
    matching_work_ids := ARRAY(
        SELECT
            work.id
        FROM
            works AS work
        WHERE
            work.work_type_id = p_work_type_id
            AND work.name = p_work_name
        FOR UPDATE
    );

    IF cardinality(matching_work_ids) = 0 THEN
        found_match_type := no_work;
    ELSIF cardinality(matching_work_ids) > 1 THEN
        found_match_type := several_works;
    ELSIF EXISTS (
        SELECT
        FROM
            work_images AS image
        WHERE
            image.work_id = matching_work_ids[1]
    ) THEN
        found_match_type := work_with_images;
    ELSE
        -- arrays count from 1. On an imageless work, appending makes it the first and primary image
        PERFORM append_work_image(
            matching_work_ids[1],
            p_storage_key,
            p_original_file_name,
            p_width,
            p_height,
            p_blur_data_uri,
            p_sha256
        );

        found_match_type := one_imageless_work;
    END IF;

    RETURN QUERY
    SELECT
        found_match_type,
        matching_work_ids;
END;
$$;
