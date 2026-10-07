DROP FUNCTION IF EXISTS get_sites;

-- Every site, for the operator: its hosts, its owner's user id (NULL once the owner deleted their
-- account, for the rest of the grace period that started) and how many admins it has
CREATE FUNCTION get_sites () RETURNS TABLE (
    site_id int,
    created_at timestamptz,
    erase_at timestamptz,
    example_sort_order int,
    main_host text,
    other_hosts text[],
    owner_user_id text,
    admin_count int
) LANGUAGE sql STABLE AS $$
SELECT
    site.id,
    site.created_at,
    site.erase_at,
    site.example_sort_order,
    main_host.host,
    -- an empty array, not NULL, for a site with only its main host
    ARRAY(
        SELECT
            other_host.host
        FROM
            site_hosts AS other_host
        WHERE
            other_host.site_id = site.id
            AND NOT other_host.is_main
        ORDER BY
            other_host.host
    )::text[],
    -- 1 is SiteRole.Owner, 2 SiteRole.Admin
    (
        SELECT
            owner.user_id
        FROM
            site_members AS owner
        WHERE
            owner.site_id = site.id
            AND owner.role = 1
    ),
    (
        SELECT
            COUNT(*)::int
        FROM
            site_members AS admin
        WHERE
            admin.site_id = site.id
            AND admin.role = 2
    )
FROM
    sites AS site
    JOIN site_hosts AS main_host ON main_host.site_id = site.id
    AND main_host.is_main;
$$;
