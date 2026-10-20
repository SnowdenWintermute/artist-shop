DROP FUNCTION IF EXISTS reorder_row_slots;

-- p_slot_ids is every slot in the row, left to right in the new order. The row's widths stay by position,
-- so a slot moved into a wider place is drawn wider
CREATE FUNCTION reorder_row_slots (p_row_id int, p_slot_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- the lock add_row_slot takes, so a slot added meanwhile can't slip past the check
    PERFORM 1 FROM page_rows WHERE id = p_row_id FOR NO KEY UPDATE;

    -- exactly the row's slots, or another tab changed them; the second count also rules out a repeat
    IF NOT FOUND
    OR cardinality(p_slot_ids) <> (SELECT COUNT(*) FROM row_slots WHERE row_id = p_row_id)
    OR cardinality(p_slot_ids) <> (
        SELECT
            COUNT(*)
        FROM
            row_slots
        WHERE
            row_id = p_row_id
            AND id = ANY (p_slot_ids)
    ) THEN
        RAISE EXCEPTION 'The row has changed since the page loaded.' USING ERRCODE = 'SH024';
    END IF;

    -- one statement, and the (row_id, sort_order) constraint is DEFERRABLE, so two slots can swap places
    UPDATE row_slots
    SET
        sort_order = ordered.position - 1
    FROM
        unnest(p_slot_ids) WITH ORDINALITY AS ordered (id, position)
    WHERE
        row_slots.id = ordered.id;
END;
$$;
