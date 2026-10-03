DROP FUNCTION IF EXISTS set_site_member_input_value;

-- Read from site_members rather than inserted outright, so a user who stopped being a member since
-- their page loaded saves nothing, where the foreign key would have failed the insert
CREATE FUNCTION set_site_member_input_value (p_site_id int, p_user_id text, p_input_name text, p_value text) RETURNS void LANGUAGE sql AS $$
INSERT INTO
    site_member_input_values (site_id, user_id, input_name, value)
SELECT
    site_member.site_id,
    site_member.user_id,
    p_input_name,
    p_value
FROM
    site_members AS site_member
WHERE
    site_member.site_id = p_site_id
    AND site_member.user_id = p_user_id
ON CONFLICT (site_id, user_id, input_name) DO UPDATE
SET
    value = excluded.value;
$$;
