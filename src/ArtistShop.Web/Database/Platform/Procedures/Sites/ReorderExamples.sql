DROP FUNCTION IF EXISTS reorder_example_sites;

-- p_site_ids is every example site, in the new order
CREATE FUNCTION reorder_example_sites (p_site_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- add_example_site takes the same lock, so no example can be added between the check and the update
    LOCK TABLE sites IN SHARE ROW EXCLUSIVE MODE;

    -- the list must be exactly the examples there are, or one was added or removed after the page
    -- loaded. An array has no primary key to rule out a repeated id, so the second count is of the
    -- distinct examples it matched: both equal to the list's length means the same set
    IF cardinality(p_site_ids) <> (
        SELECT
            COUNT(*)
        FROM
            sites
        WHERE
            example_sort_order IS NOT NULL
    )
    OR cardinality(p_site_ids) <> (
        SELECT
            COUNT(*)
        FROM
            sites
        WHERE
            example_sort_order IS NOT NULL
            AND id = ANY (p_site_ids)
    ) THEN
        RAISE EXCEPTION 'The example websites have changed since the page loaded.' USING ERRCODE = 'SH017';
    END IF;

    -- WITH ORDINALITY numbers each element by its place in the array, from 1
    UPDATE sites
    SET
        example_sort_order = ordered.position - 1
    FROM
        unnest(p_site_ids) WITH ORDINALITY AS ordered (id, position)
    WHERE
        sites.id = ordered.id;
END;
$$;
