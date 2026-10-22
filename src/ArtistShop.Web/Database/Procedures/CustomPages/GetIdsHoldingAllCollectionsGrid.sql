DROP FUNCTION IF EXISTS get_custom_page_ids_holding_all_collections_grid;

-- the pages that can be the collections page: those with a card grid of all collections anywhere on them
CREATE FUNCTION get_custom_page_ids_holding_all_collections_grid () RETURNS TABLE (id int) LANGUAGE sql STABLE AS $$
SELECT DISTINCT
    page_rows.page_id
FROM
    page_rows
    JOIN row_slots ON row_slots.row_id = page_rows.id
    JOIN slot_placements ON slot_placements.slot_id = row_slots.id
    JOIN custom_components ON custom_components.id = slot_placements.component_id
WHERE
    -- CustomComponentType.CardGrid showing CardGridSourceType.AllCollections or CollectionsAndLooseWorks,
    -- which lists every collection before the works in none
    custom_components.type = 1
    AND custom_components.settings #>> '{source,type}' IN ('AllCollections', 'CollectionsAndLooseWorks');
$$;
