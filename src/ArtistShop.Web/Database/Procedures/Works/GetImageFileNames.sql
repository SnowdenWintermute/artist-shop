DROP FUNCTION IF EXISTS get_work_image_file_names;

-- the name each image was uploaded under, so the bulk upload can leave out a file a work
-- already has an image from. An image saved without one isn't listed
CREATE FUNCTION get_work_image_file_names (p_work_ids int[]) RETURNS TABLE (work_id int, original_file_name text) LANGUAGE sql STABLE AS $$
SELECT
    image.work_id,
    image.original_file_name::text
FROM
    work_images AS image
WHERE
    image.work_id = ANY (p_work_ids)
    AND image.original_file_name IS NOT NULL;
$$;
