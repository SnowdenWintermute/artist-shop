DROP FUNCTION IF EXISTS set_page_row_layout;

-- p_slot_count is how many slots the page saw, which p_widths was chosen for
CREATE FUNCTION set_page_row_layout (p_row_id int, p_slot_count int, p_widths int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    PERFORM 1 FROM page_rows WHERE id = p_row_id FOR NO KEY UPDATE;

    IF NOT FOUND OR p_slot_count <> (SELECT COUNT(*) FROM row_slots WHERE row_id = p_row_id) THEN
        RAISE EXCEPTION 'The row has changed since the page loaded.' USING ERRCODE = 'SH024';
    END IF;

    UPDATE page_rows
    SET
        widths = p_widths
    WHERE
        id = p_row_id;
END;
$$;
