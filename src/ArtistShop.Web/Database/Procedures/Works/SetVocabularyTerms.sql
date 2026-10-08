DROP FUNCTION IF EXISTS set_work_vocabulary_terms;

-- A term row carries nothing of its own, so replacing all of them is simpler than working out a
-- difference. On a new work the DELETE finds nothing and this is only the insert.
CREATE FUNCTION set_work_vocabulary_terms (p_work_id int, p_work_type_id int, p_vocabulary_term_ids int[]) RETURNS void LANGUAGE sql AS $$
DELETE FROM work_and_vocabulary_terms_junction
WHERE
    work_id = p_work_id;

-- vocabulary_id and is_mutually_exclusive are looked up from each term rather than passed in, and
-- work_type_id is copied in, so the foreign keys and the mutually exclusive index on the junction can
-- check the row. If a term's vocabulary isn't checked for this type, the insert fails with a foreign
-- key violation
INSERT INTO
    work_and_vocabulary_terms_junction (work_id, work_type_id, term_id, vocabulary_id, is_mutually_exclusive)
SELECT
    p_work_id,
    p_work_type_id,
    term.id,
    term.vocabulary_id,
    vocabulary.is_mutually_exclusive
FROM
    vocabulary_terms AS term
    JOIN vocabularies AS vocabulary ON vocabulary.id = term.vocabulary_id
WHERE
    term.id = ANY (p_vocabulary_term_ids);
$$;
