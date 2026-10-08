DROP FUNCTION IF EXISTS get_all_work_image_storage_keys;

CREATE FUNCTION get_all_work_image_storage_keys () RETURNS TABLE (storage_key text) LANGUAGE sql STABLE AS $$
SELECT
    image.storage_key
FROM
    work_images AS image;
$$;
