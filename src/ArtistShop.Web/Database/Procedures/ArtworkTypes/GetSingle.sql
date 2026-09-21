-- A function returns one result set, so the type and its fields are two functions, and the
-- repository calls both in one command
DROP FUNCTION IF EXISTS get_artwork_type;

CREATE FUNCTION get_artwork_type (p_id int) RETURNS TABLE (id int, name text) LANGUAGE sql STABLE AS $$
SELECT
    artwork_type.id,
    artwork_type.name
FROM
    artwork_types AS artwork_type
WHERE
    artwork_type.id = p_id;
$$;

DROP FUNCTION IF EXISTS get_artwork_type_field_ids;

CREATE FUNCTION get_artwork_type_field_ids (p_id int) RETURNS TABLE (artwork_field_id int) LANGUAGE sql STABLE AS $$
SELECT
    junction.artwork_field_id
FROM
    artwork_type_and_artwork_fields_junction AS junction
WHERE
    junction.artwork_type_id = p_id;
$$;
