DROP FUNCTION IF EXISTS get_collection_by_slug;

CREATE FUNCTION get_collection_by_slug (p_slug text) RETURNS TABLE (id int, name text, slug text) LANGUAGE sql STABLE AS $$
SELECT
    collections.id,
    collections.name,
    collections.slug
FROM
    collections
WHERE
    collections.slug = p_slug;
$$;
