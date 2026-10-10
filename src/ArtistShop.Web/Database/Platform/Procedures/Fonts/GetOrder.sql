DROP FUNCTION IF EXISTS get_font_order;

CREATE FUNCTION get_font_order () RETURNS TABLE (font text) LANGUAGE sql STABLE AS $$
SELECT
    font_order.font
FROM
    font_order
ORDER BY
    font_order.sort_order;
$$;
