DROP FUNCTION IF EXISTS get_vocabulary_works_with_several_terms;

-- A LANGUAGE sql body is checked when it's created, so vocabulary_work_ids_with_several_terms has to
-- exist already: its file, FindWorkIdsWithSeveralTerms.sql, sorts before this one
CREATE FUNCTION get_vocabulary_works_with_several_terms (p_id int) RETURNS TABLE (name text) LANGUAGE sql STABLE AS $$
SELECT
    work.name
FROM
    works AS work
    JOIN vocabulary_work_ids_with_several_terms(p_id) AS several ON several.work_id = work.id;
$$;
