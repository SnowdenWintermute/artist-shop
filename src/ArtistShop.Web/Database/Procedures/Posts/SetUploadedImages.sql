DROP FUNCTION IF EXISTS set_post_uploaded_images;

-- Rebuilds the post's library image rows from its body, as set_post_works does its works.
-- plpgsql, so rich_text_image_storage_keys is looked up when it runs and the files can run in any order
CREATE FUNCTION set_post_uploaded_images (p_post_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM post_and_uploaded_images_junction
    WHERE
        post_id = p_post_id;

    INSERT INTO
        post_and_uploaded_images_junction (post_id, uploaded_image_id)
    SELECT DISTINCT
        p_post_id,
        image.id
    FROM
        posts AS post
        CROSS JOIN LATERAL rich_text_image_storage_keys(post.body) AS embedded (storage_key)
        -- the save refuses an image no longer in the library, but one deleted since the check is skipped
        JOIN uploaded_images AS image ON image.storage_key = embedded.storage_key
    WHERE
        post.id = p_post_id;
END;
$$;
