DROP FUNCTION IF EXISTS set_custom_component_uploaded_images;

-- Rebuilds a text component's library image rows from its text, as set_post_uploaded_images does a
-- post's. Any other type has no "body", so it has none. plpgsql, so rich_text_image_storage_keys is
-- looked up when it runs and the files can run in any order
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
        CROSS JOIN LATERAL rich_text_image_storage_keys(component.settings -> 'body') AS embedded (storage_key)
        JOIN uploaded_images AS image ON image.storage_key = embedded.storage_key
    WHERE
        component.id = p_component_id;
END;
$$;
