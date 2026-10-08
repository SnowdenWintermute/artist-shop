DROP FUNCTION IF EXISTS get_work_id_by_slug;

-- NULL for an unknown slug. The repository then reads the work by its id
CREATE FUNCTION get_work_id_by_slug (p_slug text) RETURNS int LANGUAGE sql STABLE AS $$
SELECT
    work.id
FROM
    works AS work
WHERE
    work.slug = p_slug;
$$;
