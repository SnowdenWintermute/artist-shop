DROP FUNCTION IF EXISTS remove_site_admin;

-- an owner removing an admin, or an admin leaving. Only an admin's row (role 2) is deleted, so the
-- owner is never removed this way
CREATE FUNCTION remove_site_admin (p_site_id int, p_user_id text) RETURNS void LANGUAGE sql AS $$
DELETE FROM site_members
WHERE
    site_id = p_site_id
    AND user_id = p_user_id
    AND role = 2;
$$;
