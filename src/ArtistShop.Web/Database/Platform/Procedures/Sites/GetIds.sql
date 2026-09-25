DROP FUNCTION IF EXISTS get_site_ids;

CREATE FUNCTION get_site_ids () RETURNS TABLE (id int) LANGUAGE sql STABLE AS $$
SELECT
    site.id
FROM
    sites AS site;
$$;
