DROP FUNCTION IF EXISTS delete_artwork_type;

CREATE FUNCTION delete_artwork_type (p_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- PERFORM runs a SELECT and throws the rows away. FOR UPDATE locks the type row until the call
    -- ends, and an artwork being added takes a share lock on it through its foreign key, so the two
    -- wait for each other here and no artwork can arrive between the check and the delete
    PERFORM
    FROM
        artwork_types
    WHERE
        id = p_id
    FOR UPDATE;

    IF EXISTS (
        SELECT
        FROM
            artworks
        WHERE
            artwork_type_id = p_id
    ) THEN
        RAISE EXCEPTION 'The artwork type is used by artworks.' USING ERRCODE = 'SH014';
    END IF;

    DELETE FROM artwork_type_and_artwork_fields_junction
    WHERE
        artwork_type_id = p_id;

    DELETE FROM vocabulary_and_artwork_types_junction
    WHERE
        artwork_type_id = p_id;

    DELETE FROM artwork_types
    WHERE
        id = p_id;
END;
$$;
