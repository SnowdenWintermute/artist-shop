DROP FUNCTION IF EXISTS get_all_custom_component_image_storage_keys;

-- Every image uploaded into an article that its text still names, so the orphan sweep keeps them.
-- plpgsql, so post_body_image_storage_keys is looked up when it runs and the
-- files can run in any order
CREATE FUNCTION get_all_custom_component_image_storage_keys () RETURNS TABLE (storage_key text) LANGUAGE plpgsql STABLE AS $$
BEGIN
    RETURN QUERY
    SELECT DISTINCT
        embedded.storage_key
    FROM
        custom_components AS component
        CROSS JOIN LATERAL post_body_image_storage_keys(component.settings -> 'body') AS embedded (storage_key);
END;
$$;
