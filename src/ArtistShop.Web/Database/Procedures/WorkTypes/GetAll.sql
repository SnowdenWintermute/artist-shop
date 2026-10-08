DROP FUNCTION IF EXISTS get_work_types;

CREATE FUNCTION get_work_types () RETURNS TABLE (id int, name text) LANGUAGE sql STABLE AS $$
SELECT
    work_type.id,
    work_type.name
FROM
    work_types AS work_type;
$$;
