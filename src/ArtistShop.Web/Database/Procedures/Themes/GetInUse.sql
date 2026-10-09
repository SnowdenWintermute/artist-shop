DROP FUNCTION IF EXISTS get_theme_in_use;

-- the saved theme's settings come along, so a public page needs one call
CREATE FUNCTION get_theme_in_use () RETURNS TABLE (preset int, theme_id int, settings jsonb) LANGUAGE sql STABLE AS $$
SELECT
    theme_in_use.preset,
    theme_in_use.theme_id,
    themes.settings
FROM
    theme_in_use
    LEFT JOIN themes ON themes.id = theme_in_use.theme_id;
$$;
