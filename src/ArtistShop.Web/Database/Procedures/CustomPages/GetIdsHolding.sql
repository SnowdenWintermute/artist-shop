DROP FUNCTION IF EXISTS get_custom_page_ids_holding;

-- the pages with a component of type p_type (CustomComponentType's id) anywhere on them
CREATE FUNCTION get_custom_page_ids_holding (p_type int) RETURNS TABLE (id int) LANGUAGE sql STABLE AS $$
SELECT DISTINCT
    page_rows.page_id
FROM
    page_rows
    JOIN row_slots ON row_slots.row_id = page_rows.id
    JOIN slot_placements ON slot_placements.slot_id = row_slots.id
    JOIN custom_components ON custom_components.id = slot_placements.component_id
WHERE
    custom_components.type = p_type;
$$;
