DROP FUNCTION IF EXISTS get_vocabulary;

CREATE FUNCTION get_vocabulary (p_id int) RETURNS TABLE (id int, name text, is_mutually_exclusive boolean) LANGUAGE sql STABLE AS $$
SELECT
    vocabulary.id,
    vocabulary.name,
    vocabulary.is_mutually_exclusive
FROM
    vocabularies AS vocabulary
WHERE
    vocabulary.id = p_id;
$$;

DROP FUNCTION IF EXISTS get_vocabulary_work_type_ids;

CREATE FUNCTION get_vocabulary_work_type_ids (p_id int) RETURNS TABLE (work_type_id int) LANGUAGE sql STABLE AS $$
SELECT
    applies.work_type_id
FROM
    vocabulary_and_work_types_junction AS applies
WHERE
    applies.vocabulary_id = p_id;
$$;
