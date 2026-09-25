DROP FUNCTION IF EXISTS get_site_invites;

-- the site's invitations, expired or not
CREATE FUNCTION get_site_invites (p_site_id int) RETURNS TABLE (email text, invited_at timestamptz, expires_at timestamptz) LANGUAGE sql STABLE AS $$
SELECT
    site_invite.email,
    site_invite.invited_at,
    site_invite.expires_at
FROM
    site_invites AS site_invite
WHERE
    site_invite.site_id = p_site_id;
$$;
