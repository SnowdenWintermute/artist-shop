DROP FUNCTION IF EXISTS delete_work_type;

CREATE FUNCTION delete_work_type (p_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- PERFORM runs a SELECT and throws the rows away. FOR UPDATE locks the type row until the call
    -- ends, and a work being added takes a share lock on it through its foreign key, so the two
    -- wait for each other here and no work can arrive between the check and the delete
    PERFORM
    FROM
        work_types
    WHERE
        id = p_id
    FOR UPDATE;

    IF EXISTS (
        SELECT
        FROM
            works
        WHERE
            work_type_id = p_id
    ) THEN
        RAISE EXCEPTION 'The work type is used by works.' USING ERRCODE = 'SH014';
    END IF;

    DELETE FROM work_type_and_work_fields_junction
    WHERE
        work_type_id = p_id;

    DELETE FROM vocabulary_and_work_types_junction
    WHERE
        work_type_id = p_id;

    DELETE FROM work_types
    WHERE
        id = p_id;
END;
$$;
