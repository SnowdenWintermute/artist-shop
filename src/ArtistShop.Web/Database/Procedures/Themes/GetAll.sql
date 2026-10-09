DROP FUNCTION IF EXISTS get_all_themes;

CREATE FUNCTION get_all_themes () RETURNS TABLE (id int, name text, settings jsonb) LANGUAGE sql STABLE AS $$
SELECT
    themes.id,
    themes.name,
    themes.settings
FROM
    themes;
$$;
