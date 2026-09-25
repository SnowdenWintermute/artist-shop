DROP FUNCTION IF EXISTS add_site_invite;

-- inviting an email that already has an invitation to the site replaces it, starting a new expiry
CREATE FUNCTION add_site_invite (p_site_id int, p_email text, p_expires_at timestamptz) RETURNS void LANGUAGE sql AS $$
INSERT INTO
    site_invites (site_id, email, expires_at)
VALUES
    (p_site_id, p_email, p_expires_at)
ON CONFLICT (site_id, email) DO UPDATE
SET
    invited_at = clock_timestamp(),
    expires_at = excluded.expires_at;
$$;
