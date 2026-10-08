DROP FUNCTION IF EXISTS count_work_type_works;

-- COUNT returns bigint in Postgres, so each is cast to the int the C# record holds
CREATE FUNCTION count_work_type_works (p_id int) RETURNS TABLE (
    total int,
    with_date_created int,
    with_height_and_width int,
    with_depth int,
    with_duration int
) LANGUAGE sql STABLE AS $$
-- COUNT(column) skips NULLs, so each counts the works with a value in that field
SELECT
    COUNT(*)::int,
    COUNT(work.date_created)::int,
    COUNT(work.height_cm)::int,
    COUNT(work.depth_cm)::int,
    COUNT(work.duration_seconds)::int
FROM
    works AS work
WHERE
    work.work_type_id = p_id;
$$;
