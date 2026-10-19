DROP FUNCTION IF EXISTS is_custom_component_in_edge_to_edge_row;

-- whether the component sits in any row whose content runs edge to edge (RowContentWidth.EdgeToEdge)
CREATE FUNCTION is_custom_component_in_edge_to_edge_row (p_component_id int) RETURNS boolean LANGUAGE sql STABLE AS $$
SELECT
    EXISTS (
        SELECT
        FROM
            slot_placements
            JOIN row_slots ON row_slots.id = slot_placements.slot_id
            JOIN page_rows ON page_rows.id = row_slots.row_id
        WHERE
            slot_placements.component_id = p_component_id
            AND page_rows.content_width = 2
    );
$$;
