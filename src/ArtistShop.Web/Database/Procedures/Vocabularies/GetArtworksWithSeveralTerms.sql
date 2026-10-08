DROP FUNCTION IF EXISTS get_vocabulary_artworks_with_several_terms;

-- A LANGUAGE sql body is checked when it's created, so vocabulary_artwork_ids_with_several_terms has to
-- exist already: its file, ArtworkIdsWithSeveralTerms.sql, sorts before this one
CREATE FUNCTION get_vocabulary_artworks_with_several_terms (p_id int) RETURNS TABLE (name text) LANGUAGE sql STABLE AS $$
SELECT
    artwork.name
FROM
    artworks AS artwork
    JOIN vocabulary_artwork_ids_with_several_terms(p_id) AS several ON several.artwork_id = artwork.id;
$$;
