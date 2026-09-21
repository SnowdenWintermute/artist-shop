DROP FUNCTION IF EXISTS add_vocabulary_term;

-- RETURNING hands back columns of the inserted row, in place of OUTPUT INSERTED
CREATE FUNCTION add_vocabulary_term (p_vocabulary_id int, p_name text) RETURNS int LANGUAGE sql AS $$
INSERT INTO
    vocabulary_terms (vocabulary_id, name)
VALUES
    (p_vocabulary_id, p_name)
RETURNING
    id;
$$;
