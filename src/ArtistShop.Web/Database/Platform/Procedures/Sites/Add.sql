DROP FUNCTION IF EXISTS add_site;

-- A new site reached by p_hosts, the first of them its main host, and owned by p_owner_user_id.
-- p_id is from reserve_site_id, taken before the site's schema was made, so the row is only added
-- for a site that's ready
CREATE FUNCTION add_site (p_id int, p_hosts text[], p_owner_user_id text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- OVERRIDING SYSTEM VALUE, since the id column is GENERATED ALWAYS
    INSERT INTO
        sites (id) OVERRIDING SYSTEM VALUE
    VALUES
        (p_id);

    -- WITH ORDINALITY numbers the array's items from 1, which is how the first is found
    INSERT INTO
        site_hosts (host, site_id, is_main)
    SELECT
        site_host.host,
        p_id,
        site_host.position = 1
    FROM
        unnest(p_hosts) WITH ORDINALITY AS site_host (host, position);

    -- in the same function, so there is never a site without an owner
    INSERT INTO
        site_members (site_id, user_id, role)
    VALUES
        (p_id, p_owner_user_id, 1);
END;
$$;
