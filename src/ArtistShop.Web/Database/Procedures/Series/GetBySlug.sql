DROP FUNCTION IF EXISTS get_series_by_slug;

CREATE FUNCTION get_series_by_slug (p_slug text) RETURNS TABLE (id int, name text, slug text) LANGUAGE sql STABLE AS $$
SELECT
    series.id,
    series.name,
    series.slug
FROM
    series
WHERE
    series.slug = p_slug;
$$;
