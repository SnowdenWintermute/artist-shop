DROP FUNCTION IF EXISTS reserve_site_id;

-- The next site's id, taken before its schema is made; add_site adds the row with it once the
-- schema is ready. A sequence never hands out an id twice, even if the site is never added
CREATE FUNCTION reserve_site_id () RETURNS int LANGUAGE sql AS $$
SELECT
    nextval(pg_get_serial_sequence('sites', 'id'))::int;
$$;
