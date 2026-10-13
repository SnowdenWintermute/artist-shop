DROP FUNCTION IF EXISTS reorder_slot_placements;

-- p_component_ids is every component in the slot, in the new order
CREATE FUNCTION reorder_slot_placements (p_slot_id int, p_component_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- the lock place_custom_component takes, so a component placed meanwhile can't slip past the check
    PERFORM 1 FROM row_slots WHERE id = p_slot_id FOR NO KEY UPDATE;

    -- exactly the slot's components, or another tab changed them; the second count also rules out a repeat
    IF NOT FOUND
    OR cardinality(p_component_ids) <> (SELECT COUNT(*) FROM slot_placements WHERE slot_id = p_slot_id)
    OR cardinality(p_component_ids) <> (
        SELECT
            COUNT(*)
        FROM
            slot_placements
        WHERE
            slot_id = p_slot_id
            AND component_id = ANY (p_component_ids)
    ) THEN
        RAISE EXCEPTION 'The slot has changed since the page loaded.' USING ERRCODE = 'SH025';
    END IF;

    -- one statement, and the primary key is DEFERRABLE, so two components can swap places
    UPDATE slot_placements
    SET
        sort_order = ordered.position - 1
    FROM
        unnest(p_component_ids) WITH ORDINALITY AS ordered (id, position)
    WHERE
        slot_placements.slot_id = p_slot_id
        AND slot_placements.component_id = ordered.id;
END;
$$;
