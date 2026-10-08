-- A function returns one result set, so the type and its fields are two functions, and the
-- repository calls both in one command
DROP FUNCTION IF EXISTS get_work_type;

CREATE FUNCTION get_work_type (p_id int) RETURNS TABLE (id int, name text) LANGUAGE sql STABLE AS $$
SELECT
    work_type.id,
    work_type.name
FROM
    work_types AS work_type
WHERE
    work_type.id = p_id;
$$;

DROP FUNCTION IF EXISTS get_work_type_field_ids;

CREATE FUNCTION get_work_type_field_ids (p_id int) RETURNS TABLE (work_field_id int) LANGUAGE sql STABLE AS $$
SELECT
    junction.work_field_id
FROM
    work_type_and_work_fields_junction AS junction
WHERE
    junction.work_type_id = p_id;
$$;
