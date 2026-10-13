DROP FUNCTION IF EXISTS place_custom_component;

-- at the bottom of the slot's stack. A component already in the slot fails
-- unique_slot_placements_slot_component
CREATE FUNCTION place_custom_component (p_slot_id int, p_component_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- the slot is the lock for its stack's order
    PERFORM 1 FROM row_slots WHERE id = p_slot_id FOR NO KEY UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The slot has changed since the page loaded.' USING ERRCODE = 'SH025';
    END IF;

    INSERT INTO slot_placements (slot_id, sort_order, component_id)
    SELECT
        p_slot_id,
        COALESCE(MAX(slot_placements.sort_order) + 1, 0),
        p_component_id
    FROM
        slot_placements
    WHERE
        slot_placements.slot_id = p_slot_id;
END;
$$;
