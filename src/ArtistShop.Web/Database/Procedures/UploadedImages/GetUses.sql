DROP FUNCTION IF EXISTS get_uploaded_image_uses;

-- The posts and text components showing any of these images, each once, with the pages a component is
-- on. type is UploadedImageUseType's id: 0 a post, 1 a component. plpgsql, so rich_text_image_storage_keys
-- is looked up when it runs and the files can run in any order
CREATE FUNCTION get_uploaded_image_uses (p_storage_keys text[]) RETURNS TABLE (type int, name text, page_names text[]) LANGUAGE plpgsql STABLE AS $$
BEGIN
    RETURN QUERY
    SELECT
        0,
        post.title::text,
        '{}'::text[]
    FROM
        posts AS post
    WHERE
        EXISTS (
            SELECT
            FROM
                rich_text_image_storage_keys(post.body) AS embedded (storage_key)
            WHERE
                embedded.storage_key = ANY (p_storage_keys)
        )
    UNION ALL
    SELECT
        1,
        component.name::text,
        COALESCE(
            (
                SELECT
                    array_agg(DISTINCT page.name::text)
                FROM
                    slot_placements AS placement
                    JOIN row_slots AS slot ON slot.id = placement.slot_id
                    JOIN page_rows AS page_row ON page_row.id = slot.row_id
                    JOIN custom_pages AS page ON page.id = page_row.page_id
                WHERE
                    placement.component_id = component.id
            ),
            '{}'
        )
    FROM
        custom_components AS component
    WHERE
        EXISTS (
            SELECT
            FROM
                rich_text_image_storage_keys(component.settings -> 'body') AS embedded (storage_key)
            WHERE
                embedded.storage_key = ANY (p_storage_keys)
        );
END;
$$;
