DROP FUNCTION IF EXISTS get_vocabulary_artworks_with_several_terms;

-- the artworks that would lose this vocabulary's terms if it became single-choice
CREATE FUNCTION get_vocabulary_artworks_with_several_terms (p_id int) RETURNS TABLE (name text) LANGUAGE sql STABLE AS $$
SELECT
    artwork.name
FROM
    artworks AS artwork
    JOIN (
        SELECT
            artwork_term.artwork_id
        FROM
            artwork_and_vocabulary_terms_junction AS artwork_term
        WHERE
            artwork_term.vocabulary_id = p_id
        GROUP BY
            artwork_term.artwork_id
        HAVING
            COUNT(*) > 1
    ) AS several ON several.artwork_id = artwork.id;
$$;
