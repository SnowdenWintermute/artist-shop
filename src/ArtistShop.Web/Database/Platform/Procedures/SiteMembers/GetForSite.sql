DROP FUNCTION IF EXISTS get_site_members;

CREATE FUNCTION get_site_members (p_site_id int) RETURNS TABLE (user_id text, role smallint) LANGUAGE sql STABLE AS $$
SELECT
    site_member.user_id,
    site_member.role
FROM
    site_members AS site_member
WHERE
    site_member.site_id = p_site_id;
$$;
