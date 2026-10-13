DROP FUNCTION IF EXISTS reorder_page_rows;

-- p_row_ids is every row on the page, in the new order
CREATE FUNCTION reorder_page_rows (p_page_id int, p_row_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- the lock add_page_row takes, so a row added meanwhile can't slip past the check
    PERFORM 1 FROM custom_pages WHERE id = p_page_id FOR NO KEY UPDATE;

    -- the list must be exactly the page's rows, or another tab changed them after this page loaded.
    -- The second count is of distinct rows matched, which also rules out a repeated id
    IF cardinality(p_row_ids) <> (SELECT COUNT(*) FROM page_rows WHERE page_id = p_page_id)
    OR cardinality(p_row_ids) <> (
        SELECT
            COUNT(*)
        FROM
            page_rows
        WHERE
            page_id = p_page_id
            AND id = ANY (p_row_ids)
    ) THEN
        RAISE EXCEPTION 'The page''s rows have changed since the page loaded.' USING ERRCODE = 'SH023';
    END IF;

    -- one statement, and the (page_id, sort_order) constraint is DEFERRABLE, so two rows can swap places
    UPDATE page_rows
    SET
        sort_order = ordered.position - 1
    FROM
        unnest(p_row_ids) WITH ORDINALITY AS ordered (id, position)
    WHERE
        page_rows.id = ordered.id;
END;
$$;
