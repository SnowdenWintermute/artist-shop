DROP FUNCTION IF EXISTS schedule_site_deletion;

-- Takes the site offline until p_erase_at, when it's erased. False when p_owner_user_id isn't its
-- owner or it's already being deleted, so nothing changed
CREATE FUNCTION schedule_site_deletion (p_site_id int, p_owner_user_id text, p_erase_at timestamptz) RETURNS boolean LANGUAGE plpgsql AS $$
BEGIN
    UPDATE sites AS site
    SET
        erase_at = p_erase_at
    WHERE
        site.id = p_site_id
        AND site.erase_at IS NULL
        -- 1 is SiteRole.Owner
        AND EXISTS (
            SELECT
                1
            FROM
                site_members AS site_member
            WHERE
                site_member.site_id = p_site_id
                AND site_member.user_id = p_owner_user_id
                AND site_member.role = 1
        );

    RETURN FOUND;
END;
$$;
