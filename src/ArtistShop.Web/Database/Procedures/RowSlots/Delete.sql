DROP FUNCTION IF EXISTS delete_row_slot;

-- Its placements go with it; its components stay in the library. p_slot_count is how many slots
-- the page saw, and p_widths the split for one fewer
CREATE FUNCTION delete_row_slot (p_row_id int, p_slot_id int, p_slot_count int, p_widths int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    PERFORM 1 FROM page_rows WHERE id = p_row_id FOR NO KEY UPDATE;

    IF NOT FOUND OR p_slot_count <> (SELECT COUNT(*) FROM row_slots WHERE row_id = p_row_id) THEN
        RAISE EXCEPTION 'The row has changed since the page loaded.' USING ERRCODE = 'SH024';
    END IF;

    DELETE FROM row_slots
    WHERE
        id = p_slot_id
        AND row_id = p_row_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The row has changed since the page loaded.' USING ERRCODE = 'SH024';
    END IF;

    UPDATE page_rows
    SET
        widths = p_widths
    WHERE
        id = p_row_id;

    PERFORM check_blog_page_has_post_list();
END;
$$;
