DROP FUNCTION IF EXISTS remove_deleted_sites_owner;

-- After p_user_id's account was deleted: its owner rows on the sites being deleted, which it owned,
-- so no member row outlives its account. Those sites have no owner for the rest of their grace
-- period, which only a site being deleted may. 1 is SiteRole.Owner
CREATE FUNCTION remove_deleted_sites_owner (p_user_id text) RETURNS void LANGUAGE sql AS $$
DELETE FROM site_members AS site_member USING sites AS site
WHERE
    site.id = site_member.site_id
    AND site_member.user_id = p_user_id
    AND site_member.role = 1
    AND site.erase_at IS NOT NULL;
$$;
