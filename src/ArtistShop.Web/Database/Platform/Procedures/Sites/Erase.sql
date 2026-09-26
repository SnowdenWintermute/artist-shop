DROP FUNCTION IF EXISTS erase_site;

-- The last step of erasing a site, after its schema and images: its row, and with it its hosts,
-- members and invitations. Only a site being deleted, so a mistake can't erase one in use
CREATE FUNCTION erase_site (p_site_id int) RETURNS void LANGUAGE sql AS $$
DELETE FROM sites
WHERE
    id = p_site_id
    AND erase_at IS NOT NULL;
$$;
