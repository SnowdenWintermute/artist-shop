DROP FUNCTION IF EXISTS get_custom_page_layout;

-- One row per placed component, with a row of nulls for an empty slot or an empty row, so the
-- page's shape survives. In order: rows top to bottom, slots left to right, stacks top to bottom
CREATE FUNCTION get_custom_page_layout (p_page_id int) RETURNS TABLE (
    row_id int,
    widths int,
    alignment int,
    top_padding int,
    bottom_padding int,
    side_padding int,
    slot_id int,
    component_id int,
    component_type int,
    component_name text,
    settings jsonb,
    settings_version int
) LANGUAGE sql STABLE AS $$
SELECT
    page_rows.id,
    page_rows.widths,
    page_rows.alignment,
    page_rows.top_padding,
    page_rows.bottom_padding,
    page_rows.side_padding,
    row_slots.id,
    custom_components.id,
    custom_components.type,
    custom_components.name,
    custom_components.settings,
    custom_components.settings_version
FROM
    page_rows
    LEFT JOIN row_slots ON row_slots.row_id = page_rows.id
    LEFT JOIN slot_placements ON slot_placements.slot_id = row_slots.id
    LEFT JOIN custom_components ON custom_components.id = slot_placements.component_id
WHERE
    page_rows.page_id = p_page_id
ORDER BY
    page_rows.sort_order,
    row_slots.sort_order,
    slot_placements.sort_order;
$$;
