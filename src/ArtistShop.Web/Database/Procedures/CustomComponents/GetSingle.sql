DROP FUNCTION IF EXISTS get_custom_component;

-- with the names of the pages it's on, none when it's on no page, and whether the blog or collections page
-- needs it. plpgsql, so it can be made before is_blog_pages_only_post_list and is_collections_pages_only_grid
CREATE FUNCTION get_custom_component (p_id int) RETURNS TABLE (
    id int,
    type int,
    name text,
    settings jsonb,
    settings_version int,
    updated_at timestamptz,
    page_names text[],
    is_blog_pages_only_post_list boolean,
    is_collections_pages_only_grid boolean
) LANGUAGE plpgsql STABLE AS $$
BEGIN
    RETURN QUERY
    SELECT
        custom_components.id,
        custom_components.type,
        custom_components.name::text,
        custom_components.settings,
        custom_components.settings_version,
        custom_components.updated_at,
        COALESCE(
            array_agg(DISTINCT custom_pages.name::text) FILTER (WHERE custom_pages.id IS NOT NULL),
            '{}'
        ),
        is_blog_pages_only_post_list(custom_components.id),
        is_collections_pages_only_grid(custom_components.id)
    FROM
        custom_components
        LEFT JOIN slot_placements ON slot_placements.component_id = custom_components.id
        LEFT JOIN row_slots ON row_slots.id = slot_placements.slot_id
        LEFT JOIN page_rows ON page_rows.id = row_slots.row_id
        LEFT JOIN custom_pages ON custom_pages.id = page_rows.page_id
    WHERE
        custom_components.id = p_id
    GROUP BY
        custom_components.id;
END;
$$;
