DROP FUNCTION IF EXISTS get_all_post_image_storage_keys;

-- Every image uploaded into a post that a post's body still names, drafts included, so the orphan
-- sweep keeps them. plpgsql, so post_body_image_storage_keys is looked up when it runs and the
-- files can run in any order
CREATE FUNCTION get_all_post_image_storage_keys () RETURNS TABLE (storage_key text) LANGUAGE plpgsql STABLE AS $$
BEGIN
    RETURN QUERY
    SELECT DISTINCT
        embedded.storage_key
    FROM
        posts AS post
        CROSS JOIN LATERAL post_body_image_storage_keys(post.body) AS embedded (storage_key);
END;
$$;
