DROP FUNCTION IF EXISTS update_custom_component;

-- A name another component has fails unique_custom_components_name
CREATE FUNCTION update_custom_component (p_id int, p_name text, p_settings jsonb, p_settings_version int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE custom_components
    SET
        name = p_name,
        settings = p_settings,
        settings_version = p_settings_version,
        updated_at = now()
    WHERE
        id = p_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The component no longer exists.' USING ERRCODE = 'SH026';
    END IF;

    PERFORM set_custom_component_works(p_id);
    PERFORM set_custom_component_uploaded_images(p_id);
END;
$$;
