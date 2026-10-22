DROP FUNCTION IF EXISTS move_slot_placement;

-- A component from one slot into another, at p_to_index in that slot's stack (its end, if past it). A component
-- already in that slot fails unique_slot_placements_slot_component
CREATE FUNCTION move_slot_placement (p_from_slot_id int, p_component_id int, p_to_slot_id int, p_to_index int) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    v_from_order int;
BEGIN
    -- both slots' locks, which place_custom_component and reorder_slot_placements take, in one order so two moves
    -- the other way round can't deadlock
    PERFORM 1 FROM row_slots WHERE id IN (p_from_slot_id, p_to_slot_id) ORDER BY id FOR NO KEY UPDATE;

    SELECT sort_order INTO v_from_order FROM slot_placements WHERE slot_id = p_from_slot_id AND component_id = p_component_id;

    IF v_from_order IS NULL OR NOT EXISTS (SELECT 1 FROM row_slots WHERE id = p_to_slot_id) THEN
        RAISE EXCEPTION 'The slot has changed since the page loaded.' USING ERRCODE = 'SH025';
    END IF;

    DELETE FROM slot_placements WHERE slot_id = p_from_slot_id AND component_id = p_component_id;

    -- one statement each, and the primary key is DEFERRABLE, so the stacks close up and open up in place
    UPDATE slot_placements SET sort_order = sort_order - 1 WHERE slot_id = p_from_slot_id AND sort_order > v_from_order;
    UPDATE slot_placements SET sort_order = sort_order + 1 WHERE slot_id = p_to_slot_id AND sort_order >= p_to_index;

    INSERT INTO slot_placements (slot_id, sort_order, component_id)
    SELECT
        p_to_slot_id,
        LEAST(p_to_index, COUNT(*)),
        p_component_id
    FROM
        slot_placements
    WHERE
        slot_id = p_to_slot_id;

    PERFORM check_blog_page_has_post_list();
    PERFORM check_collections_page_has_grid();
END;
$$;
