-- dropped first because CREATE OR REPLACE can't change a function's return columns
DROP FUNCTION IF EXISTS get_product_types;

-- STABLE promises the function only reads, which lets the planner treat it like a plain query
CREATE FUNCTION get_product_types () RETURNS TABLE (id int, name text, is_default boolean) LANGUAGE sql STABLE AS $$
SELECT
    product_type.id,
    product_type.name,
    product_type.is_default
FROM
    product_types AS product_type;
$$;
