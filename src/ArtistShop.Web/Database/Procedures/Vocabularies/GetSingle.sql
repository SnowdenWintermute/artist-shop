DROP FUNCTION IF EXISTS get_vocabulary;

CREATE FUNCTION get_vocabulary (p_id int) RETURNS TABLE (id int, name text) LANGUAGE sql STABLE AS $$
SELECT
    vocabulary.id,
    vocabulary.name
FROM
    vocabularies AS vocabulary
WHERE
    vocabulary.id = p_id;
$$;

DROP FUNCTION IF EXISTS get_vocabulary_artwork_type_ids;

CREATE FUNCTION get_vocabulary_artwork_type_ids (p_id int) RETURNS TABLE (artwork_type_id int) LANGUAGE sql STABLE AS $$
SELECT
    applies.artwork_type_id
FROM
    vocabulary_and_artwork_types_junction AS applies
WHERE
    applies.vocabulary_id = p_id;
$$;
