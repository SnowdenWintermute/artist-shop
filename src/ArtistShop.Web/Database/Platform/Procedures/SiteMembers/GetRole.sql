DROP FUNCTION IF EXISTS get_site_member_role;

-- the user's role on the site, or no row when they aren't one of its members
CREATE FUNCTION get_site_member_role (p_site_id int, p_user_id text) RETURNS TABLE (role smallint) LANGUAGE sql STABLE AS $$
SELECT
    site_member.role
FROM
    site_members AS site_member
WHERE
    site_member.site_id = p_site_id
    AND site_member.user_id = p_user_id;
$$;
