DROP FUNCTION IF EXISTS delete_vocabulary;

-- a sql function may hold several statements; with no variables or checks it needs no plpgsql
CREATE FUNCTION delete_vocabulary (p_id int) RETURNS void LANGUAGE sql AS $$
DELETE FROM artwork_and_vocabulary_terms_junction
WHERE
    vocabulary_id = p_id;

DELETE FROM vocabulary_terms
WHERE
    vocabulary_id = p_id;

DELETE FROM vocabulary_and_artwork_types_junction
WHERE
    vocabulary_id = p_id;

DELETE FROM vocabularies
WHERE
    id = p_id;
$$;
