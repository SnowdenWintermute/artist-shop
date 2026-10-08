DROP FUNCTION IF EXISTS get_work_names;

-- one work type's titles, with each one's slug. A photograph and a screenshot can share a title:
-- they are different works, and only a title this type already has is a repeat. The same rule the
-- bulk image uploader matches file names by
CREATE FUNCTION get_work_names (p_work_type_id int) RETURNS TABLE (name text, slug text) LANGUAGE sql STABLE AS $$
SELECT
    work.name,
    work.slug
FROM
    works AS work
WHERE
    work.work_type_id = p_work_type_id;
$$;
