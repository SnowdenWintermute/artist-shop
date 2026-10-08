DROP FUNCTION IF EXISTS update_work_type;

CREATE FUNCTION update_work_type (p_id int, p_name text, p_work_field_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    -- must match the WorkField enum in C#
    date_created_field_id CONSTANT int := 1;
    height_and_width_field_id CONSTANT int := 2;
    depth_field_id CONSTANT int := 3;
    duration_field_id CONSTANT int := 4;
BEGIN
    -- first, so this locks the type row before any work row, in the same order as add_work
    -- and update_work
    UPDATE work_types
    SET
        name = p_name
    WHERE
        id = p_id;

    -- FOUND is plpgsql's "did the last statement touch a row", in place of @@ROWCOUNT
    IF NOT FOUND THEN
        RAISE EXCEPTION 'The work type no longer exists.' USING ERRCODE = 'SH010';
    END IF;

    -- a switched-off field's values are cleared, so a work only holds values for its type's
    -- fields. Depth before height and width: check_works_depth_needs_height_and_width checks
    -- each row as it changes
    IF NOT depth_field_id = ANY (p_work_field_ids) THEN
        UPDATE works
        SET
            depth_cm = NULL
        WHERE
            work_type_id = p_id
            AND depth_cm IS NOT NULL;
    END IF;

    IF NOT height_and_width_field_id = ANY (p_work_field_ids) THEN
        UPDATE works
        SET
            height_cm = NULL,
            width_cm = NULL
        WHERE
            work_type_id = p_id
            AND height_cm IS NOT NULL;
    END IF;

    IF NOT date_created_field_id = ANY (p_work_field_ids) THEN
        UPDATE works
        SET
            date_created = NULL,
            date_created_precision = NULL
        WHERE
            work_type_id = p_id
            AND date_created IS NOT NULL;
    END IF;

    IF NOT duration_field_id = ANY (p_work_field_ids) THEN
        UPDATE works
        SET
            duration_seconds = NULL
        WHERE
            work_type_id = p_id
            AND duration_seconds IS NOT NULL;
    END IF;

    -- one statement each: Postgres checks a foreign key at the end of the statement, so depth and
    -- height and width can go (or arrive) together
    DELETE FROM work_type_and_work_fields_junction AS junction
    WHERE
        junction.work_type_id = p_id
        AND NOT junction.work_field_id = ANY (p_work_field_ids);

    -- ON CONFLICT DO NOTHING skips a field the type already has, in place of a NOT EXISTS check
    INSERT INTO
        work_type_and_work_fields_junction (work_type_id, work_field_id, requires_work_field_id)
    SELECT
        p_id,
        work_field.id,
        work_field.requires_work_field_id
    FROM
        work_fields AS work_field
    WHERE
        work_field.id = ANY (p_work_field_ids)
    ON CONFLICT (work_type_id, work_field_id) DO NOTHING;
END;
$$;
