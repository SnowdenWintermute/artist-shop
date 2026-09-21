DROP FUNCTION IF EXISTS add_artwork_type;

-- plpgsql rather than sql because it needs a variable. The call is one transaction already, so
-- there's no BEGIN TRANSACTION
CREATE FUNCTION add_artwork_type (p_name text, p_artwork_field_ids int[]) RETURNS int LANGUAGE plpgsql AS $$
DECLARE
    new_id int;
BEGIN
    INSERT INTO
        artwork_types (name)
    VALUES
        (p_name)
    RETURNING
        id INTO new_id;

    -- a field whose required field isn't in the list fails the junction's self-referencing foreign
    -- key, which Postgres checks at the end of the statement
    INSERT INTO
        artwork_type_and_artwork_fields_junction (artwork_type_id, artwork_field_id, requires_artwork_field_id)
    SELECT
        new_id,
        artwork_field.id,
        artwork_field.requires_artwork_field_id
    FROM
        artwork_fields AS artwork_field
    WHERE
        artwork_field.id = ANY (p_artwork_field_ids);

    RETURN new_id;
END;
$$;
