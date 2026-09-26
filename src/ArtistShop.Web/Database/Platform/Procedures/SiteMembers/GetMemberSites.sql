DROP FUNCTION IF EXISTS get_member_sites;

-- the sites p_user_id is a member of, each with its main host, the user's role there, and when it's
-- erased if its owner deleted it
CREATE FUNCTION get_member_sites (p_user_id text) RETURNS TABLE (site_id int, main_host text, role smallint, erase_at timestamptz) LANGUAGE sql STABLE AS $$
SELECT
    site_member.site_id,
    site_host.host,
    site_member.role,
    site.erase_at
FROM
    site_members AS site_member
    JOIN site_hosts AS site_host ON site_host.site_id = site_member.site_id
    AND site_host.is_main
    JOIN sites AS site ON site.id = site_member.site_id
WHERE
    site_member.user_id = p_user_id;
$$;
