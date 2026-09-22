DROP FUNCTION IF EXISTS get_vocabulary_terms_with_usage;

CREATE FUNCTION get_vocabulary_terms_with_usage (p_vocabulary_id int) RETURNS TABLE (id int, name text, artwork_count int) LANGUAGE sql STABLE AS $$
SELECT
    term.id,
    term.name,
    -- not COUNT(*), because a term with no artworks still has its one joined row
    COUNT(artwork_term.artwork_id)::int
FROM
    vocabulary_terms AS term
    -- a LEFT JOIN keeps the terms no artwork uses
    LEFT JOIN artwork_and_vocabulary_terms_junction AS artwork_term ON artwork_term.term_id = term.id
WHERE
    term.vocabulary_id = p_vocabulary_id
GROUP BY
    term.id,
    term.name;
$$;
