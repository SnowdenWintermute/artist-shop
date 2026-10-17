DROP FUNCTION IF EXISTS set_page_row_padding;

CREATE FUNCTION set_page_row_padding (p_row_id int, p_top int, p_bottom int, p_sides int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE page_rows
    SET
        top_padding = p_top,
        bottom_padding = p_bottom,
        side_padding = p_sides
    WHERE
        id = p_row_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The row has changed since the page loaded.' USING ERRCODE = 'SH024';
    END IF;
END;
$$;
