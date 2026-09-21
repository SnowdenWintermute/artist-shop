DROP FUNCTION IF EXISTS check_artwork_choices_are_current;

-- Everything a form offered could have been changed in another tab while it sat open. The locks
-- taken here last until the caller's transaction ends, so nothing can switch a field off or
-- delete a choice between this check and the write.
CREATE FUNCTION check_artwork_choices_are_current (
    p_artwork_type_id int,
    p_date_created date,
    p_height_cm numeric,
    p_width_cm numeric,
    p_depth_cm numeric,
    p_duration_seconds int,
    p_vocabulary_term_ids int[],
    p_series_ids int[]
) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    -- must match the ArtworkField enum in C#
    date_created_field_id CONSTANT int := 1;
    height_and_width_field_id CONSTANT int := 2;
    depth_field_id CONSTANT int := 3;
    duration_field_id CONSTANT int := 4;
    enabled_field_ids int[];
BEGIN
    -- FOR SHARE blocks any UPDATE or DELETE of the row. update_artwork_type and delete_artwork_type
    -- both lock the type row before anything else, so they wait for this transaction
    PERFORM
    FROM
        artwork_types
    WHERE
        id = p_artwork_type_id
    FOR SHARE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The artwork type no longer exists.' USING ERRCODE = 'SH010';
    END IF;

    -- ARRAY(subquery) collects a one-column result into an array
    enabled_field_ids := ARRAY(
        SELECT
            artwork_field_id
        FROM
            artwork_type_and_artwork_fields_junction
        WHERE
            artwork_type_id = p_artwork_type_id
    );

    -- a value for a field the type doesn't have means it was switched off while the form was open
    IF (
        p_date_created IS NOT NULL
        AND NOT date_created_field_id = ANY (enabled_field_ids)
    )
    OR (
        COALESCE(p_height_cm, p_width_cm) IS NOT NULL
        AND NOT height_and_width_field_id = ANY (enabled_field_ids)
    )
    OR (
        p_depth_cm IS NOT NULL
        AND NOT depth_field_id = ANY (enabled_field_ids)
    )
    OR (
        p_duration_seconds IS NOT NULL
        AND NOT duration_field_id = ANY (enabled_field_ids)
    ) THEN
        RAISE EXCEPTION 'A field was switched off for this artwork type.' USING ERRCODE = 'SH011';
    END IF;

    -- A join at write time would silently skip a term id that no longer exists (say it was deleted
    -- in another tab while this form was open). Checking first turns that into a loud error.
    -- FOR KEY SHARE is the lightest row lock: it blocks a DELETE but lets a rename through
    PERFORM
    FROM
        vocabulary_terms
    WHERE
        id = ANY (p_vocabulary_term_ids)
    FOR KEY SHARE;

    IF EXISTS (
        SELECT
        FROM
            unnest(p_vocabulary_term_ids) AS chosen (id)
        WHERE
            NOT EXISTS (
                SELECT
                FROM
                    vocabulary_terms AS term
                WHERE
                    term.id = chosen.id
            )
    ) THEN
        RAISE EXCEPTION 'A chosen vocabulary term no longer exists.' USING ERRCODE = 'SH001';
    END IF;

    -- the junction's foreign key would catch a deleted series too, but as a plain foreign key
    -- violation, which says nothing about which choice was stale
    PERFORM
    FROM
        series
    WHERE
        id = ANY (p_series_ids)
    FOR KEY SHARE;

    IF EXISTS (
        SELECT
        FROM
            unnest(p_series_ids) AS chosen (id)
        WHERE
            NOT EXISTS (
                SELECT
                FROM
                    series
                WHERE
                    series.id = chosen.id
            )
    ) THEN
        RAISE EXCEPTION 'A chosen series no longer exists.' USING ERRCODE = 'SH004';
    END IF;
END;
$$;
