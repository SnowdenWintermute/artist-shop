DROP FUNCTION IF EXISTS delete_uploaded_images;

-- Deletes the images, taking their embeds out of every post and text component in the same
-- transaction. The posts' and components' updated_at stay as they were: the artist didn't edit them
-- (Mike, 2026-10-15). The files are left for the orphan sweep, as a deleted work's are
CREATE FUNCTION delete_uploaded_images (p_storage_keys text[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE posts AS post
    SET
        body = rich_text_without_images(post.body, p_storage_keys)
    WHERE
        EXISTS (
            SELECT
            FROM
                rich_text_image_storage_keys(post.body) AS embedded (storage_key)
            WHERE
                embedded.storage_key = ANY (p_storage_keys)
        );

    UPDATE custom_components AS component
    SET
        settings = jsonb_set(component.settings, '{body}', rich_text_without_images(component.settings -> 'body', p_storage_keys))
    WHERE
        EXISTS (
            SELECT
            FROM
                rich_text_image_storage_keys(component.settings -> 'body') AS embedded (storage_key)
            WHERE
                embedded.storage_key = ANY (p_storage_keys)
        );

    DELETE FROM uploaded_images
    WHERE
        storage_key = ANY (p_storage_keys);
END;
$$;
