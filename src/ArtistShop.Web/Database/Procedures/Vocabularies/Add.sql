DROP FUNCTION IF EXISTS add_vocabulary;

CREATE FUNCTION add_vocabulary (p_name text, p_is_mutually_exclusive boolean, p_work_type_ids int[]) RETURNS int LANGUAGE plpgsql AS $$
DECLARE
    new_id int;
BEGIN
    INSERT INTO
        vocabularies (name, is_mutually_exclusive)
    VALUES
        (p_name, p_is_mutually_exclusive)
    RETURNING
        id INTO new_id;

    -- reading from work_types drops a type deleted in another tab while the form was open:
    -- nothing is lost, because no work can have that type any more
    INSERT INTO
        vocabulary_and_work_types_junction (vocabulary_id, work_type_id)
    SELECT
        new_id,
        work_type.id
    FROM
        work_types AS work_type
    WHERE
        work_type.id = ANY (p_work_type_ids);

    RETURN new_id;
END;
$$;
