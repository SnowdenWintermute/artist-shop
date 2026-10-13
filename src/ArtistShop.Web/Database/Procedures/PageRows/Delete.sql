DROP FUNCTION IF EXISTS delete_page_row;

-- its slots and placements go with it; its components stay in the library. A row that's gone
-- already does nothing
CREATE FUNCTION delete_page_row (p_row_id int) RETURNS void LANGUAGE sql AS $$
DELETE FROM page_rows
WHERE
    id = p_row_id;
$$;
