DROP FUNCTION IF EXISTS append_work_image;

-- After the work's other images, and its primary image when it has none. The work's row is
-- locked, so two uploads for one work take turns and each sees the other's image. When the
-- work already has an image with this hash, as after a retried upload or a second import of the
-- same folder, it adds nothing and returns that image instead, so a retry gets the same answer
CREATE FUNCTION append_work_image (
    p_work_id int,
    p_storage_key text,
    p_original_file_name text,
    p_width int,
    p_height int,
    p_blur_data_uri text,
    p_sha256 text
) RETURNS TABLE (
    storage_key text,
    original_file_name text,
    width int,
    height int,
    blur_data_uri text,
    appended boolean
) LANGUAGE plpgsql AS $$
BEGIN
    PERFORM
    FROM
        works
    WHERE
        works.id = p_work_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The work no longer exists.' USING ERRCODE = 'SH003';
    END IF;

    RETURN QUERY
    SELECT
        image.storage_key::text,
        image.original_file_name::text,
        image.width,
        image.height,
        image.blur_data_uri::text,
        false
    FROM
        work_images AS image
    WHERE
        image.work_id = p_work_id
        AND image.sha256 = p_sha256
    LIMIT
        1;

    IF FOUND THEN
        RETURN;
    END IF;

    -- with no images, max is null and count is 0, so it's first and primary
    INSERT INTO
        work_images (
            work_id,
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
        p_work_id,
        p_storage_key,
        p_original_file_name,
        COALESCE(max(image.sort_order) + 1, 0),
        count(*) = 0,
        p_width,
        p_height,
        p_blur_data_uri,
        p_sha256
    FROM
        work_images AS image
    WHERE
        image.work_id = p_work_id;

    RETURN QUERY
    SELECT
        p_storage_key,
        p_original_file_name,
        p_width,
        p_height,
        p_blur_data_uri,
        true;
END;
$$;
