DROP FUNCTION IF EXISTS delete_vocabulary_term;

CREATE FUNCTION delete_vocabulary_term (p_id int) RETURNS void LANGUAGE sql AS $$
DELETE FROM artwork_and_vocabulary_terms_junction
WHERE
    term_id = p_id;

DELETE FROM vocabulary_terms
WHERE
    id = p_id;
$$;
