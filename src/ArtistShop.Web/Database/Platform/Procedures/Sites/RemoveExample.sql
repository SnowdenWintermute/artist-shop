DROP FUNCTION IF EXISTS remove_example_site;

-- the gap it leaves in the order is harmless: only the order matters, not the numbers
CREATE FUNCTION remove_example_site (p_site_id int) RETURNS void LANGUAGE sql AS $$
UPDATE sites
SET
    example_sort_order = NULL
WHERE
    id = p_site_id;
$$;
