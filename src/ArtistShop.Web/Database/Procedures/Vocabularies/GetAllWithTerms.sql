DROP FUNCTION IF EXISTS get_vocabularies_with_terms;

CREATE FUNCTION get_vocabularies_with_terms (p_work_type_id int) RETURNS TABLE (
    id int,
    name text,
    is_mutually_exclusive boolean,
    term_id int,
    term_name text
) LANGUAGE sql STABLE AS $$
-- one row per term; a vocabulary with no terms still gets one row, with NULL term columns.
-- No work type means every vocabulary, which is what the work list filters across
SELECT
    vocabulary.id,
    vocabulary.name,
    vocabulary.is_mutually_exclusive,
    term.id,
    term.name
FROM
    vocabularies AS vocabulary
    LEFT JOIN vocabulary_terms AS term ON term.vocabulary_id = vocabulary.id
WHERE
    p_work_type_id IS NULL
    OR EXISTS (
        SELECT
        FROM
            vocabulary_and_work_types_junction AS applies
        WHERE
            applies.vocabulary_id = vocabulary.id
            AND applies.work_type_id = p_work_type_id
    );
$$;
