DROP FUNCTION IF EXISTS update_vocabulary;

CREATE FUNCTION update_vocabulary (p_id int, p_name text, p_is_single_choice boolean, p_artwork_type_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- FOR UPDATE holds off check_artwork_choices_are_current, which locks the vocabularies of the
    -- chosen terms, so no artwork gains a second term between the DELETE below and the UPDATE
    PERFORM
    FROM
        vocabularies
    WHERE
        id = p_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The vocabulary no longer exists.' USING ERRCODE = 'SH002';
    END IF;

    -- An artwork with several of the terms has all of them removed, since there's no telling which
    -- one the artist meant to keep. It goes first: the UPDATE cascades the flag into the junction,
    -- and the single-choice index refuses it while any artwork still has two
    IF p_is_single_choice THEN
        DELETE FROM artwork_and_vocabulary_terms_junction AS artwork_term
        WHERE
            artwork_term.vocabulary_id = p_id
            AND artwork_term.artwork_id IN (
                SELECT
                    several.artwork_id
                FROM
                    artwork_and_vocabulary_terms_junction AS several
                WHERE
                    several.vocabulary_id = p_id
                GROUP BY
                    several.artwork_id
                HAVING
                    COUNT(*) > 1
            );
    END IF;

    UPDATE vocabularies
    SET
        name = p_name,
        is_single_choice = p_is_single_choice
    WHERE
        id = p_id;

    -- the terms come off the artworks first; the foreign key refuses removing a type from the
    -- vocabulary while an artwork of that type still uses one of its terms
    DELETE FROM artwork_and_vocabulary_terms_junction AS artwork_term
    WHERE
        artwork_term.vocabulary_id = p_id
        AND NOT artwork_term.artwork_type_id = ANY (p_artwork_type_ids);

    DELETE FROM vocabulary_and_artwork_types_junction AS applies
    WHERE
        applies.vocabulary_id = p_id
        AND NOT applies.artwork_type_id = ANY (p_artwork_type_ids);

    -- reading from artwork_types drops a type deleted in another tab, as in add_vocabulary
    INSERT INTO
        vocabulary_and_artwork_types_junction (vocabulary_id, artwork_type_id)
    SELECT
        p_id,
        artwork_type.id
    FROM
        artwork_types AS artwork_type
    WHERE
        artwork_type.id = ANY (p_artwork_type_ids)
    ON CONFLICT (vocabulary_id, artwork_type_id) DO NOTHING;
END;
$$;
