DROP FUNCTION IF EXISTS set_artwork_vocabulary_terms;

-- A term row carries nothing of its own, so replacing all of them is simpler than working out a
-- difference. On a new artwork the DELETE finds nothing and this is only the insert.
CREATE FUNCTION set_artwork_vocabulary_terms (p_artwork_id int, p_artwork_type_id int, p_vocabulary_term_ids int[]) RETURNS void LANGUAGE sql AS $$
DELETE FROM artwork_and_vocabulary_terms_junction
WHERE
    artwork_id = p_artwork_id;

-- vocabulary_id is looked up from each term rather than passed in, and artwork_type_id is copied
-- in, so the three foreign keys on the junction can check the row. If a term's vocabulary isn't
-- ticked for this type, the insert fails with a foreign key violation
INSERT INTO
    artwork_and_vocabulary_terms_junction (artwork_id, artwork_type_id, term_id, vocabulary_id)
SELECT
    p_artwork_id,
    p_artwork_type_id,
    term.id,
    term.vocabulary_id
FROM
    vocabulary_terms AS term
WHERE
    term.id = ANY (p_vocabulary_term_ids);
$$;
