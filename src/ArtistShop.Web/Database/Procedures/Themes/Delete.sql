DROP FUNCTION IF EXISTS delete_theme;

-- the theme in use goes back to Paper through theme_in_use's ON DELETE SET NULL
CREATE FUNCTION delete_theme (p_id int) RETURNS void LANGUAGE sql AS $$
DELETE FROM themes
WHERE
    id = p_id;
$$;
