DROP FUNCTION IF EXISTS get_uploaded_image_uses;

-- The posts and text components showing any of these images, each once, with the pages a component is
-- on. type is UploadedImageUseType's id: 0 a post, 1 a component. Image components showing them are
-- deleted with them, so get_image_components_showing lists those
CREATE FUNCTION get_uploaded_image_uses (p_storage_keys text[]) RETURNS TABLE (type int, name text, page_names text[]) LANGUAGE sql STABLE AS $$
SELECT
    0,
    post.title::text,
    '{}'::text[]
FROM
    posts AS post
WHERE
    post.id IN (
        SELECT
            post_image.post_id
        FROM
            post_and_uploaded_images_junction AS post_image
            JOIN uploaded_images AS image ON image.id = post_image.uploaded_image_id
        WHERE
            image.storage_key = ANY (p_storage_keys)
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
$$;
