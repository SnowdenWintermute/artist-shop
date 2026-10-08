DROP FUNCTION IF EXISTS get_vocabularies_without_work_types;

CREATE FUNCTION get_vocabularies_without_work_types () RETURNS TABLE (id int, name text) LANGUAGE sql STABLE AS $$
SELECT
    vocabulary.id,
    vocabulary.name
FROM
    vocabularies AS vocabulary
WHERE
    NOT EXISTS (
        SELECT
        FROM
            vocabulary_and_work_types_junction AS applies
        WHERE
            applies.vocabulary_id = vocabulary.id
    );
$$;
