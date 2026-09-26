DROP FUNCTION IF EXISTS keep_site;

-- Stops the site's deletion, putting it back online. False when p_owner_user_id isn't its owner or
-- it isn't being deleted, such as after it was erased, so nothing changed
CREATE FUNCTION keep_site (p_site_id int, p_owner_user_id text) RETURNS boolean LANGUAGE plpgsql AS $$
BEGIN
    UPDATE sites AS site
    SET
        erase_at = NULL
    WHERE
        site.id = p_site_id
        AND site.erase_at IS NOT NULL
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
