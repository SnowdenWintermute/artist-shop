DROP FUNCTION IF EXISTS get_all_custom_components;

-- with the names of the pages each is on, none for one on no page
CREATE FUNCTION get_all_custom_components () RETURNS TABLE (
    id int,
    type int,
    name text,
    settings jsonb,
    settings_version int,
    updated_at timestamptz,
    page_names text[]
) LANGUAGE sql STABLE AS $$
SELECT
    custom_components.id,
    custom_components.type,
    custom_components.name,
    custom_components.settings,
    custom_components.settings_version,
    custom_components.updated_at,
    COALESCE(
        array_agg(DISTINCT custom_pages.name::text) FILTER (WHERE custom_pages.id IS NOT NULL),
        '{}'
    )
FROM
    custom_components
    LEFT JOIN slot_placements ON slot_placements.component_id = custom_components.id
    LEFT JOIN row_slots ON row_slots.id = slot_placements.slot_id
    LEFT JOIN page_rows ON page_rows.id = row_slots.row_id
    LEFT JOIN custom_pages ON custom_pages.id = page_rows.page_id
GROUP BY
    custom_components.id;
$$;
