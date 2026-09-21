DROP FUNCTION IF EXISTS get_all_series;

-- SELECT * FROM a function returns its rows in the order the function produced them
CREATE FUNCTION get_all_series () RETURNS TABLE (id int, name text, slug text) LANGUAGE sql STABLE AS $$
SELECT
    series.id,
    series.name,
    series.slug
FROM
    series
ORDER BY
    series.sort_order;
$$;
