DROP FUNCTION IF EXISTS get_work_fields;

CREATE FUNCTION get_work_fields () RETURNS TABLE (id int, name text, requires_work_field_id int) LANGUAGE sql STABLE AS $$
SELECT
    work_field.id,
    work_field.name,
    work_field.requires_work_field_id
FROM
    work_fields AS work_field;
$$;
