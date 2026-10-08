DROP FUNCTION IF EXISTS add_work_type;

-- plpgsql rather than sql because it needs a variable. The call is one transaction already, so
-- there's no BEGIN TRANSACTION
CREATE FUNCTION add_work_type (p_name text, p_work_field_ids int[]) RETURNS int LANGUAGE plpgsql AS $$
DECLARE
    new_id int;
BEGIN
    INSERT INTO
        work_types (name)
    VALUES
        (p_name)
    RETURNING
        id INTO new_id;

    -- a field whose required field isn't in the list fails the junction's self-referencing foreign
    -- key, which Postgres checks at the end of the statement
    INSERT INTO
        work_type_and_work_fields_junction (work_type_id, work_field_id, requires_work_field_id)
    SELECT
        new_id,
        work_field.id,
        work_field.requires_work_field_id
    FROM
        work_fields AS work_field
    WHERE
        work_field.id = ANY (p_work_field_ids);

    RETURN new_id;
END;
$$;
