DROP FUNCTION IF EXISTS add_custom_component_to_slot;

-- A new component starts in the slot it was made for, in one step, so a slot deleted meanwhile
-- leaves no component behind that nobody placed
-- A name another component has fails unique_custom_components_name
CREATE FUNCTION add_custom_component_to_slot (p_slot_id int, p_type int, p_name text, p_settings jsonb, p_settings_version int) RETURNS int LANGUAGE plpgsql AS $$
DECLARE
    v_component_id int;
BEGIN
    INSERT INTO custom_components (type, name, settings, settings_version)
    VALUES (p_type, p_name, p_settings, p_settings_version)
    RETURNING id INTO v_component_id;

    PERFORM place_custom_component(p_slot_id, v_component_id);
    PERFORM set_custom_component_works(v_component_id);
    PERFORM set_custom_component_uploaded_images(v_component_id);

    RETURN v_component_id;
END;
$$;
