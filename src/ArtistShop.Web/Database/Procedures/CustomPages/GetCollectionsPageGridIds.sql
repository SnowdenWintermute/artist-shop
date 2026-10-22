DROP FUNCTION IF EXISTS get_collections_page_grid_ids;

-- the card grids of all collections on the collections page, which must always hold one
-- (check_collections_page_has_grid)
CREATE FUNCTION get_collections_page_grid_ids () RETURNS TABLE (id int) LANGUAGE sql STABLE AS $$
SELECT DISTINCT
    custom_components.id
FROM
    collections_page
    JOIN page_rows ON page_rows.page_id = collections_page.page_id
    JOIN row_slots ON row_slots.row_id = page_rows.id
    JOIN slot_placements ON slot_placements.slot_id = row_slots.id
    JOIN custom_components ON custom_components.id = slot_placements.component_id
WHERE
    -- CustomComponentType.CardGrid showing CardGridSourceType.AllCollections or CollectionsAndLooseWorks,
    -- which lists every collection before the works in none
    custom_components.type = 1
    AND custom_components.settings #>> '{source,type}' IN ('AllCollections', 'CollectionsAndLooseWorks');
$$;
