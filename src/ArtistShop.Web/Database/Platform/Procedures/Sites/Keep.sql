DROP FUNCTION IF EXISTS keep_site;

-- Stops the site's deletion, putting it back online. False when p_owner_user_id isn't its owner, it
-- isn't being deleted, or its erase_at has come by p_now, so nothing changed. The last is the
-- opposite of get_sites_due_for_erasing, so a site SiteEraser has begun erasing can't be kept
-- partway. p_now is the app's clock, so tests can move it
CREATE FUNCTION keep_site (p_site_id int, p_owner_user_id text, p_now timestamptz) RETURNS boolean LANGUAGE plpgsql AS $$
BEGIN
    UPDATE sites AS site
    SET
        erase_at = NULL
    WHERE
        site.id = p_site_id
        AND site.erase_at > p_now
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
