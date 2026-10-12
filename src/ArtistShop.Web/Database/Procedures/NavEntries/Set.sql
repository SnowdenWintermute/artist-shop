DROP FUNCTION IF EXISTS set_nav_entries;

-- The whole nav, in order. A page deleted since the list was loaded fails
-- foreign_key_nav_entries_custom_pages
CREATE FUNCTION set_nav_entries (p_page_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM nav_entries;

    INSERT INTO
        nav_entries (page_id, sort_order)
    SELECT
        entries.page_id,
        entries.position
    FROM
        unnest(p_page_ids) WITH ORDINALITY AS entries (page_id, position);
END;
$$;
