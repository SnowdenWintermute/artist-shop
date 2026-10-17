DROP FUNCTION IF EXISTS get_image_components_showing;

-- The image components showing any of these works or library images, with the pages each is on, which
-- deleting them deletes too. Read from the junction rows, so before the works or images go
CREATE FUNCTION get_image_components_showing (p_work_ids int[], p_storage_keys text[]) RETURNS TABLE (
    id int,
    name text,
    page_names text[]
) LANGUAGE sql STABLE AS $$
SELECT
    component.id,
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
    -- CustomComponentType.Image
    component.type = 4
    AND (
        EXISTS (
            SELECT
            FROM
                custom_component_and_works_junction AS junction
            WHERE
                junction.component_id = component.id
                AND junction.work_id = ANY (p_work_ids)
        )
        OR EXISTS (
            SELECT
            FROM
                custom_component_and_uploaded_images_junction AS component_image
                JOIN uploaded_images AS image ON image.id = component_image.uploaded_image_id
            WHERE
                component_image.component_id = component.id
                AND image.storage_key = ANY (p_storage_keys)
        )
    );
$$;
