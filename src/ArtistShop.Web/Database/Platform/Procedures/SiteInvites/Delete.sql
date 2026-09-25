DROP FUNCTION IF EXISTS delete_site_invite;

-- revoking or declining; nothing happens if it's already gone
CREATE FUNCTION delete_site_invite (p_site_id int, p_email text) RETURNS void LANGUAGE sql AS $$
DELETE FROM site_invites
WHERE
    site_id = p_site_id
    AND email = p_email;
$$;
