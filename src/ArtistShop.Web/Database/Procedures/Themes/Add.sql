DROP FUNCTION IF EXISTS add_theme;

-- a name another theme has fails unique_themes_name
CREATE FUNCTION add_theme (p_name text, p_settings jsonb) RETURNS int LANGUAGE sql AS $$
INSERT INTO
    themes (name, settings)
VALUES
    (p_name, p_settings)
RETURNING
    id;
$$;
