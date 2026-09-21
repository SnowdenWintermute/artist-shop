DROP FUNCTION IF EXISTS update_artwork_type;

CREATE FUNCTION update_artwork_type (p_id int, p_name text, p_artwork_field_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    -- must match the ArtworkField enum in C#
    date_created_field_id CONSTANT int := 1;
    height_and_width_field_id CONSTANT int := 2;
    depth_field_id CONSTANT int := 3;
    duration_field_id CONSTANT int := 4;
BEGIN
    -- first, so this locks the type row before anything else, in the same order as add_artwork
    UPDATE artwork_types
    SET
        name = p_name
    WHERE
        id = p_id;

    -- FOUND is plpgsql's "did the last statement touch a row", in place of @@ROWCOUNT
    IF NOT FOUND THEN
        RAISE EXCEPTION 'The artwork type no longer exists.' USING ERRCODE = 'SH010';
    END IF;

    -- a switched-off field's values are cleared, so an artwork only holds values for its type's
    -- fields. Depth before height and width: check_artworks_depth_needs_height_and_width checks
    -- each row as it changes
    IF NOT depth_field_id = ANY (p_artwork_field_ids) THEN
        UPDATE artworks
        SET
            depth_cm = NULL
        WHERE
            artwork_type_id = p_id
            AND depth_cm IS NOT NULL;
    END IF;

    IF NOT height_and_width_field_id = ANY (p_artwork_field_ids) THEN
        UPDATE artworks
        SET
            height_cm = NULL,
            width_cm = NULL
        WHERE
            artwork_type_id = p_id
            AND height_cm IS NOT NULL;
    END IF;

    IF NOT date_created_field_id = ANY (p_artwork_field_ids) THEN
        UPDATE artworks
        SET
            date_created = NULL,
            date_created_precision = NULL
        WHERE
            artwork_type_id = p_id
            AND date_created IS NOT NULL;
    END IF;

    IF NOT duration_field_id = ANY (p_artwork_field_ids) THEN
        UPDATE artworks
        SET
            duration_seconds = NULL
        WHERE
            artwork_type_id = p_id
            AND duration_seconds IS NOT NULL;
    END IF;

    -- one statement each: Postgres checks a foreign key at the end of the statement, so depth and
    -- height and width can go (or arrive) together
    DELETE FROM artwork_type_and_artwork_fields_junction AS junction
    WHERE
        junction.artwork_type_id = p_id
        AND NOT junction.artwork_field_id = ANY (p_artwork_field_ids);

    -- ON CONFLICT DO NOTHING skips a field the type already has, in place of a NOT EXISTS check
    INSERT INTO
        artwork_type_and_artwork_fields_junction (artwork_type_id, artwork_field_id, requires_artwork_field_id)
    SELECT
        p_id,
        artwork_field.id,
        artwork_field.requires_artwork_field_id
    FROM
        artwork_fields AS artwork_field
    WHERE
        artwork_field.id = ANY (p_artwork_field_ids)
    ON CONFLICT (artwork_type_id, artwork_field_id) DO NOTHING;
END;
$$;
