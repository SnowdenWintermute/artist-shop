DROP FUNCTION IF EXISTS add_vocabulary;

CREATE FUNCTION add_vocabulary (p_name text, p_artwork_type_ids int[]) RETURNS int LANGUAGE plpgsql AS $$
DECLARE
    new_id int;
BEGIN
    INSERT INTO
        vocabularies (name)
    VALUES
        (p_name)
    RETURNING
        id INTO new_id;

    -- reading from artwork_types drops a type deleted in another tab while the form was open:
    -- nothing is lost, because no artwork can have that type any more
    INSERT INTO
        vocabulary_and_artwork_types_junction (vocabulary_id, artwork_type_id)
    SELECT
        new_id,
        artwork_type.id
    FROM
        artwork_types AS artwork_type
    WHERE
        artwork_type.id = ANY (p_artwork_type_ids);

    RETURN new_id;
END;
$$;
