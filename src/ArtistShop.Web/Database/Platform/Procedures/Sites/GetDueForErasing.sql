DROP FUNCTION IF EXISTS get_sites_due_for_erasing;

-- p_now is the app's clock, so tests can move it
CREATE FUNCTION get_sites_due_for_erasing (p_now timestamptz) RETURNS TABLE (id int) LANGUAGE sql STABLE AS $$
SELECT
    site.id
FROM
    sites AS site
WHERE
    site.erase_at <= p_now;
$$;
