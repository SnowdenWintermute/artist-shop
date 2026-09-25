DROP FUNCTION IF EXISTS get_site_hosts;

-- every site's hosts, which the app keeps in memory to find the site each request is for
CREATE FUNCTION get_site_hosts () RETURNS TABLE (host text, site_id int, is_main boolean) LANGUAGE sql STABLE AS $$
SELECT
    site_host.host,
    site_host.site_id,
    site_host.is_main
FROM
    site_hosts AS site_host;
$$;
