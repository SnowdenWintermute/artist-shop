DROP FUNCTION IF EXISTS delete_theme;

-- a website using the theme moves onto p_preset
CREATE FUNCTION delete_theme (p_id int, p_preset int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- FOR UPDATE holds off use_theme, which could otherwise put the theme in use between the
    -- UPDATE and the DELETE
    PERFORM 1 FROM themes WHERE id = p_id FOR UPDATE;

    UPDATE theme_in_use
    SET
        theme_id = NULL,
        preset = p_preset
    WHERE
        theme_id = p_id;

    DELETE FROM themes
    WHERE
        id = p_id;
END;
$$;
