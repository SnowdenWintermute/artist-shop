DROP FUNCTION IF EXISTS get_colors;

CREATE FUNCTION get_colors () RETURNS TABLE (color_role_id int, color text) LANGUAGE sql STABLE AS $$
SELECT
    colors.color_role_id,
    colors.color
FROM
    colors;
$$;
