DROP FUNCTION IF EXISTS get_site_hosts;

-- The hosts of every site that's online, which the app keeps in memory to find the site each
-- request is for. A deleted site's are left out, so they're a 404 until it's erased
CREATE FUNCTION get_site_hosts () RETURNS TABLE (host text, site_id int, is_main boolean) LANGUAGE sql STABLE AS $$
SELECT
    site_host.host,
    site_host.site_id,
    site_host.is_main
FROM
    site_hosts AS site_host
    JOIN sites AS site ON site.id = site_host.site_id
WHERE
    site.erase_at IS NULL;
$$;
