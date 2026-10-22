DROP FUNCTION IF EXISTS move_row_slot;

-- A slot with its stack from one row into another, at p_to_index (its end, if past it). The slot counts are how
-- many slots the page saw in each row, and the widths the split each row takes for its new count. A row left with
-- no slots gets an empty one, so a row is never without
CREATE FUNCTION move_row_slot (
    p_slot_id int,
    p_from_row_id int,
    p_from_slot_count int,
    p_from_widths int,
    p_to_row_id int,
    p_to_slot_count int,
    p_to_widths int,
    p_to_index int
) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    v_from_order int;
BEGIN
    -- both rows' locks, which add_row_slot and reorder_row_slots take, in one order so two moves the other way
    -- round can't deadlock
    PERFORM 1 FROM page_rows WHERE id IN (p_from_row_id, p_to_row_id) ORDER BY id FOR NO KEY UPDATE;

    SELECT sort_order INTO v_from_order FROM row_slots WHERE id = p_slot_id AND row_id = p_from_row_id;

    IF v_from_order IS NULL
    OR p_from_slot_count <> (SELECT COUNT(*) FROM row_slots WHERE row_id = p_from_row_id)
    OR p_to_slot_count <> (SELECT COUNT(*) FROM row_slots WHERE row_id = p_to_row_id) THEN
        RAISE EXCEPTION 'The row has changed since the page loaded.' USING ERRCODE = 'SH024';
    END IF;

    -- out of the way first, at a place no slot has, so the rows can close up and open up around it; one statement
    -- each, and the (row_id, sort_order) constraint is DEFERRABLE
    UPDATE row_slots SET row_id = p_to_row_id, sort_order = -1 WHERE id = p_slot_id;
    UPDATE row_slots SET sort_order = sort_order - 1 WHERE row_id = p_from_row_id AND sort_order > v_from_order;
    UPDATE row_slots SET sort_order = sort_order + 1 WHERE row_id = p_to_row_id AND sort_order >= p_to_index;
    UPDATE row_slots SET sort_order = LEAST(p_to_index, p_to_slot_count) WHERE id = p_slot_id;

    IF p_from_slot_count = 1 THEN
        INSERT INTO row_slots (row_id, sort_order) VALUES (p_from_row_id, 0);
    END IF;

    UPDATE page_rows SET widths = p_from_widths WHERE id = p_from_row_id;
    UPDATE page_rows SET widths = p_to_widths WHERE id = p_to_row_id;

    PERFORM check_blog_page_has_post_list();
    PERFORM check_collections_page_has_grid();
END;
$$;
