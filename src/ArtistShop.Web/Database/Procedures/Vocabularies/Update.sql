DROP FUNCTION IF EXISTS update_vocabulary;

CREATE FUNCTION update_vocabulary (p_id int, p_name text, p_is_mutually_exclusive boolean, p_work_type_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- FOR UPDATE holds off check_work_choices_are_current, which locks the vocabularies of the
    -- chosen terms, so no work gains a second term between the DELETE below and the UPDATE
    PERFORM
    FROM
        vocabularies
    WHERE
        id = p_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The vocabulary no longer exists.' USING ERRCODE = 'SH002';
    END IF;

    -- A work with several of the terms has all of them removed, since there's no telling which
    -- one the artist meant to keep. It goes first: the UPDATE cascades the flag into the junction,
    -- and the mutually exclusive index refuses it while any work still has two
    IF p_is_mutually_exclusive THEN
        DELETE FROM work_and_vocabulary_terms_junction AS work_term
        WHERE
            work_term.vocabulary_id = p_id
            AND work_term.work_id IN (
                SELECT
                    several.work_id
                FROM
                    vocabulary_work_ids_with_several_terms(p_id) AS several
            );
    END IF;

    UPDATE vocabularies
    SET
        name = p_name,
        is_mutually_exclusive = p_is_mutually_exclusive
    WHERE
        id = p_id;

    -- the terms come off the works first; the foreign key refuses removing a type from the
    -- vocabulary while a work of that type still uses one of its terms
    DELETE FROM work_and_vocabulary_terms_junction AS work_term
    WHERE
        work_term.vocabulary_id = p_id
        AND NOT work_term.work_type_id = ANY (p_work_type_ids);

    DELETE FROM vocabulary_and_work_types_junction AS applies
    WHERE
        applies.vocabulary_id = p_id
        AND NOT applies.work_type_id = ANY (p_work_type_ids);

    -- reading from work_types drops a type deleted in another tab, as in add_vocabulary
    INSERT INTO
        vocabulary_and_work_types_junction (vocabulary_id, work_type_id)
    SELECT
        p_id,
        work_type.id
    FROM
        work_types AS work_type
    WHERE
        work_type.id = ANY (p_work_type_ids)
    ON CONFLICT (vocabulary_id, work_type_id) DO NOTHING;
END;
$$;
