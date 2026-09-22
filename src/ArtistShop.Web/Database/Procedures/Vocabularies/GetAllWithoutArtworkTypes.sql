DROP FUNCTION IF EXISTS get_vocabularies_without_artwork_types;

CREATE FUNCTION get_vocabularies_without_artwork_types () RETURNS TABLE (id int, name text) LANGUAGE sql STABLE AS $$
SELECT
    vocabulary.id,
    vocabulary.name
FROM
    vocabularies AS vocabulary
WHERE
    NOT EXISTS (
        SELECT
        FROM
            vocabulary_and_artwork_types_junction AS applies
        WHERE
            applies.vocabulary_id = vocabulary.id
    );
$$;
