DROP FUNCTION IF EXISTS get_all_custom_component_image_storage_keys;

-- Every image uploaded into an article that its text still names, so the orphan sweep keeps them.
-- An article's settings hold its text as {"body": <the editor's Delta>}, read as
-- get_all_post_image_storage_keys reads a post's
CREATE FUNCTION get_all_custom_component_image_storage_keys () RETURNS TABLE (storage_key text) LANGUAGE sql STABLE AS $$
SELECT DISTINCT
    embedded.storage_key #>> '{}'
FROM
    custom_components AS component
    CROSS JOIN LATERAL jsonb_path_query(
        component.settings,
        '$.body.ops[*].insert."artshop-image".storageKey ? (@.type() == "string")'
    ) AS embedded (storage_key);
$$;
