DROP FUNCTION IF EXISTS get_example_sites;

-- the main hosts of the example sites that are online, in the operator's order, for the home page
CREATE FUNCTION get_example_sites () RETURNS TABLE (main_host text) LANGUAGE sql STABLE AS $$
SELECT
    site_host.host
FROM
    sites AS site
    JOIN site_hosts AS site_host ON site_host.site_id = site.id
    AND site_host.is_main
WHERE
    site.example_sort_order IS NOT NULL
    AND site.erase_at IS NULL
ORDER BY
    site.example_sort_order;
$$;
