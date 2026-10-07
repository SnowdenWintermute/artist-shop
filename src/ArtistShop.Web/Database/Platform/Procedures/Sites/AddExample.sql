DROP FUNCTION IF EXISTS add_example_site;

-- Puts the site last among the examples. Nothing changes for one that already is
CREATE FUNCTION add_example_site (p_site_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- Postgres won't lock rows under an aggregate, so two additions could read the same MAX; this
    -- makes the second wait. reorder_example_sites takes the same lock
    LOCK TABLE sites IN SHARE ROW EXCLUSIVE MODE;

    UPDATE sites
    SET
        example_sort_order = (
            SELECT
                COALESCE(MAX(example.example_sort_order) + 1, 0)
            FROM
                sites AS example
        )
    WHERE
        id = p_site_id
        AND example_sort_order IS NULL;
END;
$$;
