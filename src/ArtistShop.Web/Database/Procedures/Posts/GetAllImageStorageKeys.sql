DROP FUNCTION IF EXISTS get_all_post_image_storage_keys;

-- Every image uploaded into a post that a post's body still names, drafts included, so the orphan
-- sweep keeps them. The jsonpath reads as set_post_artworks's does
CREATE FUNCTION get_all_post_image_storage_keys () RETURNS TABLE (storage_key text) LANGUAGE sql STABLE AS $$
SELECT DISTINCT
    -- #>> '{}' is the JSON string's text, without its quotes
    embedded.storage_key #>> '{}'
FROM
    posts AS post
    CROSS JOIN LATERAL jsonb_path_query(
        post.body,
        '$.ops[*].insert."artshop-image".storageKey ? (@.type() == "string")'
    ) AS embedded (storage_key);
$$;
