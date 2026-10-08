DROP FUNCTION IF EXISTS get_all_collections;

-- SELECT * FROM a function returns its rows in the order the function produced them
CREATE FUNCTION get_all_collections () RETURNS TABLE (id int, name text, slug text) LANGUAGE sql STABLE AS $$
SELECT
    collections.id,
    collections.name,
    collections.slug
FROM
    collections
ORDER BY
    collections.sort_order;
$$;
