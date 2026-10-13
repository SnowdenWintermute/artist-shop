DROP FUNCTION IF EXISTS add_row_slot;

-- An empty slot at the row's right end. p_slot_count is how many slots the page saw, and p_widths
-- the split for one more
CREATE FUNCTION add_row_slot (p_row_id int, p_slot_count int, p_widths int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- the row is the lock for its slots, so two adds can't both pass the count
    PERFORM 1 FROM page_rows WHERE id = p_row_id FOR NO KEY UPDATE;

    IF NOT FOUND OR p_slot_count <> (SELECT COUNT(*) FROM row_slots WHERE row_id = p_row_id) THEN
        RAISE EXCEPTION 'The row has changed since the page loaded.' USING ERRCODE = 'SH024';
    END IF;

    INSERT INTO row_slots (row_id, sort_order)
    SELECT
        p_row_id,
        MAX(row_slots.sort_order) + 1
    FROM
        row_slots
    WHERE
        row_slots.row_id = p_row_id;

    UPDATE page_rows
    SET
        widths = p_widths
    WHERE
        id = p_row_id;
END;
$$;
