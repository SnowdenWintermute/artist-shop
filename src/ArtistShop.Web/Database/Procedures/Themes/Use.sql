DROP FUNCTION IF EXISTS use_theme;

-- one of p_theme_id and p_preset, or neither for Paper
CREATE FUNCTION use_theme (p_theme_id int, p_preset int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    IF p_theme_id IS NOT NULL THEN
        -- FOR SHARE holds off a delete of the theme until this commits
        PERFORM 1 FROM themes WHERE id = p_theme_id FOR SHARE;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'The theme no longer exists.' USING ERRCODE = 'SH019';
        END IF;
    END IF;

    UPDATE theme_in_use
    SET
        theme_id = p_theme_id,
        preset = p_preset;
END;
$$;
