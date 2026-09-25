DROP FUNCTION IF EXISTS accept_site_invite;

-- Uses up p_email's unexpired invitation to the site and makes p_user_id one of its admins, in one
-- transaction. False when there was no such invitation, so nobody was added. A member already
-- keeps their role
CREATE FUNCTION accept_site_invite (p_site_id int, p_email text, p_user_id text) RETURNS boolean LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM site_invites
    WHERE
        site_id = p_site_id
        AND email = p_email
        AND expires_at > clock_timestamp();

    IF NOT FOUND THEN
        RETURN false;
    END IF;

    -- 2 is SiteRole.Admin
    INSERT INTO
        site_members (site_id, user_id, role)
    VALUES
        (p_site_id, p_user_id, 2)
    ON CONFLICT (site_id, user_id) DO NOTHING;

    RETURN true;
END;
$$;
