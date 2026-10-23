DROP FUNCTION IF EXISTS get_custom_components_linking_to;

-- The components linking to any of these pages, works, collections or posts, with their type and the pages
-- each is on: deleting the targets deletes the buttons and takes the others' links. Read from the link rows,
-- so before the targets go
CREATE FUNCTION get_custom_components_linking_to (p_page_ids int[], p_work_ids int[], p_collection_ids int[], p_post_ids int[]) RETURNS TABLE (
    id int,
    type int,
    name text,
    page_names text[]
) LANGUAGE sql STABLE AS $$
SELECT
    component.id,
    component.type,
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
    JOIN custom_component_links AS link ON link.component_id = component.id
WHERE
    link.page_id = ANY (p_page_ids)
    OR link.work_id = ANY (p_work_ids)
    OR link.collection_id = ANY (p_collection_ids)
    OR link.post_id = ANY (p_post_ids);
$$;
