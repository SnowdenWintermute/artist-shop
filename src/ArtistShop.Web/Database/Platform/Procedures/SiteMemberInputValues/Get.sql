DROP FUNCTION IF EXISTS get_site_member_input_value;

-- no row when the member never changed the input
CREATE FUNCTION get_site_member_input_value (p_site_id int, p_user_id text, p_input_name text) RETURNS TABLE (value text) LANGUAGE sql STABLE AS $$
SELECT
    input_value.value
FROM
    site_member_input_values AS input_value
WHERE
    input_value.site_id = p_site_id
    AND input_value.user_id = p_user_id
    AND input_value.input_name = p_input_name;
$$;
