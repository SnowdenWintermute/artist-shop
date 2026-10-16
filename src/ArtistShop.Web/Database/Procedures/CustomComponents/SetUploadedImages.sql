DROP FUNCTION IF EXISTS set_custom_component_uploaded_images;

-- Rebuilds a component's library image rows: a text component's from its text, as
-- set_post_uploaded_images does a post's, and an image component's from a library source. Any other type
-- has neither a "body" nor a library source, so it has none. plpgsql, so
-- rich_text_image_storage_keys is looked up when it runs and the files can run in any order
CREATE FUNCTION set_custom_component_uploaded_images (p_component_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM custom_component_and_uploaded_images_junction
    WHERE
        component_id = p_component_id;

    INSERT INTO
        custom_component_and_uploaded_images_junction (component_id, uploaded_image_id)
    SELECT DISTINCT
        p_component_id,
        image.id
    FROM
        custom_components AS component
        CROSS JOIN LATERAL (
            SELECT
                embedded.storage_key
            FROM
                rich_text_image_storage_keys(component.settings -> 'body') AS embedded (storage_key)
            UNION ALL
            SELECT
                component.settings #>> '{source,storageKey}'
            WHERE
                component.settings #>> '{source,type}' = 'Library'
        ) AS used (storage_key)
        JOIN uploaded_images AS image ON image.storage_key = used.storage_key
    WHERE
        component.id = p_component_id;
END;
$$;
