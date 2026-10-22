DROP FUNCTION IF EXISTS work_main_image_key;

-- The storage key of the image a work shows when nothing names one: its primary image, or else its
-- first. NULL for a work with no images. STABLE: it reads tables, so only one statement may rely on
-- the answer staying the same
CREATE FUNCTION work_main_image_key (p_work_id int) RETURNS text LANGUAGE sql STABLE AS $$
SELECT
    image.storage_key
FROM
    work_images AS image
WHERE
    image.work_id = p_work_id
ORDER BY
    image.is_primary DESC,
    image.sort_order
LIMIT
    1;
$$;
