DROP FUNCTION IF EXISTS delete_expired_site_invites;

-- every site's invitations that expired before p_before, which their owners no longer need to see
CREATE FUNCTION delete_expired_site_invites (p_before timestamptz) RETURNS void LANGUAGE sql AS $$
DELETE FROM site_invites
WHERE
    expires_at < p_before;
$$;
