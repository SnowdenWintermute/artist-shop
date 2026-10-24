DROP FUNCTION IF EXISTS get_work_type_settings;

CREATE FUNCTION get_work_type_settings () RETURNS TABLE (dimension_display int) LANGUAGE sql STABLE AS $$
SELECT
    work_type_settings.dimension_display
FROM
    work_type_settings;
$$;
