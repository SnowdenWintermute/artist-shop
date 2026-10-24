DROP FUNCTION IF EXISTS update_work_type_settings;

CREATE FUNCTION update_work_type_settings (p_dimension_display int) RETURNS void LANGUAGE sql AS $$
UPDATE work_type_settings
SET
    dimension_display = p_dimension_display;
$$;
