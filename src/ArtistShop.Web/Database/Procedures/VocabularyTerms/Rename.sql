DROP FUNCTION IF EXISTS rename_vocabulary_term;

CREATE FUNCTION rename_vocabulary_term (p_id int, p_name text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE vocabulary_terms
    SET
        name = p_name
    WHERE
        id = p_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The vocabulary term no longer exists.' USING ERRCODE = 'SH001';
    END IF;
END;
$$;
