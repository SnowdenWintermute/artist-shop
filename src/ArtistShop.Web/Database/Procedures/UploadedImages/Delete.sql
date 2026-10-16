DROP FUNCTION IF EXISTS delete_uploaded_images;

-- Deletes the images, taking their embeds out of every post and text component, and emptying every
-- image component showing one, in the same transaction. The posts' and components' updated_at stay as they were: the artist didn't edit them
-- (Mike, 2026-10-15). Deleting the rows takes their junction rows with them. The files are left for
-- the orphan sweep, as a deleted work's are
CREATE FUNCTION delete_uploaded_images (p_storage_keys text[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE posts AS post
    SET
        body = rich_text_without_images(post.body, p_storage_keys)
    WHERE
        post.id IN (
            SELECT
                post_image.post_id
            FROM
                post_and_uploaded_images_junction AS post_image
                JOIN uploaded_images AS image ON image.id = post_image.uploaded_image_id
            WHERE
                image.storage_key = ANY (p_storage_keys)
        );

    UPDATE custom_components AS component
    SET
        settings = jsonb_set(component.settings, '{body}', rich_text_without_images(component.settings -> 'body', p_storage_keys))
    WHERE
        component.id IN (
            SELECT
                component_image.component_id
            FROM
                custom_component_and_uploaded_images_junction AS component_image
                JOIN uploaded_images AS image ON image.id = component_image.uploaded_image_id
            WHERE
                image.storage_key = ANY (p_storage_keys)
        )
        -- CustomComponentType.Text
        AND component.type = 3;

    UPDATE custom_components AS component
    SET
        settings = component.settings - 'source'
    WHERE
        -- CustomComponentType.Image
        component.type = 4
        AND component.settings #>> '{source,storageKey}' = ANY (p_storage_keys);

    DELETE FROM uploaded_images
    WHERE
        storage_key = ANY (p_storage_keys);
END;
$$;
