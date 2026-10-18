DROP FUNCTION IF EXISTS add_page_row;

-- a row at the bottom of the page, with one empty slot across its whole width
CREATE FUNCTION add_page_row (p_page_id int) RETURNS int LANGUAGE plpgsql AS $$
DECLARE
    v_row_id int;
BEGIN
    -- the page row is the lock for its rows' order, so two adds can't take the same place
    PERFORM 1 FROM custom_pages WHERE id = p_page_id FOR NO KEY UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The page no longer exists.' USING ERRCODE = 'SH020';
    END IF;

    -- RowWidths.Whole, RowAlignment.Start, RowHeight.Natural, RowContentWidth.Page, RowPadding.New, RowGap.Medium
    INSERT INTO page_rows (page_id, sort_order, widths, alignment, height, content_width, top_padding, bottom_padding, side_padding, gap)
    SELECT
        p_page_id,
        COALESCE(MAX(page_rows.sort_order) + 1, 0),
        1,
        1,
        1,
        1,
        1,
        1,
        3,
        3
    FROM
        page_rows
    WHERE
        page_rows.page_id = p_page_id
    RETURNING
        id INTO v_row_id;

    INSERT INTO row_slots (row_id, sort_order) VALUES (v_row_id, 0);

    RETURN v_row_id;
END;
$$;
