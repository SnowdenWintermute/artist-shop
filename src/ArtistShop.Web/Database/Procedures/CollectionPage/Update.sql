DROP FUNCTION IF EXISTS update_collection_page;

CREATE FUNCTION update_collection_page (p_layout int, p_cards_per_page int) RETURNS void LANGUAGE sql AS $$
UPDATE collection_page
SET
    layout = p_layout,
    cards_per_page = p_cards_per_page;
$$;
