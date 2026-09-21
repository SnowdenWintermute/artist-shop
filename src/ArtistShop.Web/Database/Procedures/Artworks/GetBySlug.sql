DROP FUNCTION IF EXISTS get_artwork_id_by_slug;

-- NULL for an unknown slug. The repository then reads the artwork by its id
CREATE FUNCTION get_artwork_id_by_slug (p_slug text) RETURNS int LANGUAGE sql STABLE AS $$
SELECT
    artwork.id
FROM
    artworks AS artwork
WHERE
    artwork.slug = p_slug;
$$;
