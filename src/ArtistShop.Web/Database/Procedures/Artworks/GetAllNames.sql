DROP FUNCTION IF EXISTS get_artwork_names;

-- one work type's titles. A photograph and a screenshot can share a title: they are different
-- works, and only a title this type already has is a repeat. The same rule the bulk image
-- uploader matches file names by
CREATE FUNCTION get_artwork_names (p_artwork_type_id int) RETURNS TABLE (name text) LANGUAGE sql STABLE AS $$
SELECT
    artwork.name
FROM
    artworks AS artwork
WHERE
    artwork.artwork_type_id = p_artwork_type_id;
$$;
