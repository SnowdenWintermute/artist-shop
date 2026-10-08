DROP FUNCTION IF EXISTS add_or_link_vocabulary;

-- For the work table's vocabulary dialog, where the artist names a vocabulary for the type they are
-- editing: a vocabulary with that name, which the collation matches regardless of case, gains the type
-- instead of the name being refused. Returns the vocabulary's id either way
CREATE FUNCTION add_or_link_vocabulary (p_name text, p_work_type_id int) RETURNS int LANGUAGE plpgsql AS $$
DECLARE
    linked_id int;
BEGIN
    INSERT INTO
        vocabularies (name)
    VALUES
        (p_name)
    ON CONFLICT ON CONSTRAINT unique_vocabularies_name DO NOTHING
    RETURNING
        id INTO linked_id;

    IF linked_id IS NULL THEN
        SELECT
            vocabulary.id
        INTO
            linked_id
        FROM
            vocabularies AS vocabulary
        WHERE
            vocabulary.name = p_name;
    END IF;

    -- as in add_vocabulary, a type deleted in another tab is left out
    INSERT INTO
        vocabulary_and_work_types_junction (vocabulary_id, work_type_id)
    SELECT
        linked_id,
        work_type.id
    FROM
        work_types AS work_type
    WHERE
        work_type.id = p_work_type_id
    ON CONFLICT DO NOTHING;

    RETURN linked_id;
END;
$$;
