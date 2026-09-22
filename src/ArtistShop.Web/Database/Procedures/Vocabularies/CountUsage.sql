DROP FUNCTION IF EXISTS count_vocabulary_terms;

CREATE FUNCTION count_vocabulary_terms (p_id int) RETURNS int LANGUAGE sql STABLE AS $$
SELECT
    COUNT(*)::int
FROM
    vocabulary_terms AS term
WHERE
    term.vocabulary_id = p_id;
$$;

DROP FUNCTION IF EXISTS count_vocabulary_artworks_by_type;

CREATE FUNCTION count_vocabulary_artworks_by_type (p_id int) RETURNS TABLE (artwork_type_id int, artwork_count int) LANGUAGE sql STABLE AS $$
SELECT
    junction.artwork_type_id,
    COUNT(DISTINCT junction.artwork_id)::int
FROM
    artwork_and_vocabulary_terms_junction AS junction
WHERE
    junction.vocabulary_id = p_id
GROUP BY
    junction.artwork_type_id;
$$;
