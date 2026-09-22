DROP FUNCTION IF EXISTS get_artwork_types;

CREATE FUNCTION get_artwork_types () RETURNS TABLE (id int, name text) LANGUAGE sql STABLE AS $$
SELECT
    artwork_type.id,
    artwork_type.name
FROM
    artwork_types AS artwork_type;
$$;
