DROP FUNCTION IF EXISTS add_site;

-- A new site reached by p_hosts, the first of them its main host, and owned by p_owner_user_id
CREATE FUNCTION add_site (p_hosts text[], p_owner_user_id text) RETURNS int LANGUAGE plpgsql AS $$
DECLARE
    new_id int;
BEGIN
    INSERT INTO
        sites DEFAULT VALUES
    RETURNING
        id INTO new_id;

    -- WITH ORDINALITY numbers the array's items from 1, which is how the first is found
    INSERT INTO
        site_hosts (host, site_id, is_main)
    SELECT
        site_host.host,
        new_id,
        site_host.position = 1
    FROM
        unnest(p_hosts) WITH ORDINALITY AS site_host (host, position);

    -- in the same function, so there is never a site without an owner
    INSERT INTO
        site_members (site_id, user_id, role)
    VALUES
        (new_id, p_owner_user_id, 1);

    RETURN new_id;
END;
$$;
