DROP FUNCTION IF EXISTS get_artwork_fields;

CREATE FUNCTION get_artwork_fields () RETURNS TABLE (id int, name text, requires_artwork_field_id int) LANGUAGE sql STABLE AS $$
SELECT
    artwork_field.id,
    artwork_field.name,
    artwork_field.requires_artwork_field_id
FROM
    artwork_fields AS artwork_field;
$$;
