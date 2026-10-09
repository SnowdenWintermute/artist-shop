DROP FUNCTION IF EXISTS update_theme;

CREATE FUNCTION update_theme (p_id int, p_name text, p_settings jsonb) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE themes
    SET
        name = p_name,
        settings = p_settings
    WHERE
        id = p_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The theme no longer exists.' USING ERRCODE = 'SH019';
    END IF;
END;
$$;
