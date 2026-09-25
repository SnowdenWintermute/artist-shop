DROP FUNCTION IF EXISTS add_site;

-- A new site reached by p_hosts, the first of them its main host
CREATE FUNCTION add_site (p_hosts text[]) RETURNS int LANGUAGE plpgsql AS $$
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

    RETURN new_id;
END;
$$;
