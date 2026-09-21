DROP FUNCTION IF EXISTS count_artwork_type_artworks;

-- COUNT returns bigint in Postgres, so each is cast to the int the C# record holds
CREATE FUNCTION count_artwork_type_artworks (p_id int) RETURNS TABLE (
    total int,
    with_date_created int,
    with_height_and_width int,
    with_depth int,
    with_duration int
) LANGUAGE sql STABLE AS $$
-- COUNT(column) skips NULLs, so each counts the artworks with a value in that field
SELECT
    COUNT(*)::int,
    COUNT(artwork.date_created)::int,
    COUNT(artwork.height_cm)::int,
    COUNT(artwork.depth_cm)::int,
    COUNT(artwork.duration_seconds)::int
FROM
    artworks AS artwork
WHERE
    artwork.artwork_type_id = p_id;
$$;
