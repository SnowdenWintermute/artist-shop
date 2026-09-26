DROP FUNCTION IF EXISTS get_email_invites;

-- the unexpired invitations for p_email to sites that aren't being deleted, each with its site's
-- main host
CREATE FUNCTION get_email_invites (p_email text) RETURNS TABLE (site_id int, main_host text, expires_at timestamptz) LANGUAGE sql STABLE AS $$
SELECT
    site_invite.site_id,
    site_host.host,
    site_invite.expires_at
FROM
    site_invites AS site_invite
    JOIN site_hosts AS site_host ON site_host.site_id = site_invite.site_id
    AND site_host.is_main
    JOIN sites AS site ON site.id = site_invite.site_id
WHERE
    site_invite.email = p_email
    AND site_invite.expires_at > clock_timestamp()
    AND site.erase_at IS NULL;
$$;
