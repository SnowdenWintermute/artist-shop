DROP FUNCTION IF EXISTS add_card_grid_chosen_collections;

-- The collections join the end of the grid's custom list, in the order given. One already in it keeps its
-- place, and one deleted since the page loaded is left out
CREATE FUNCTION add_card_grid_chosen_collections (p_component_id int, p_collection_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    last_place int;
BEGIN
    last_place := lock_card_grid_chosen_cards(p_component_id);

    INSERT INTO
        card_grid_chosen_cards (component_id, collection_id, sort_order)
    SELECT
        p_component_id,
        collections.id,
        last_place + chosen.position
    FROM
        unnest(p_collection_ids) WITH ORDINALITY AS chosen (id, position)
        JOIN collections ON collections.id = chosen.id
    ON CONFLICT (component_id, collection_id) DO NOTHING;
END;
$$;
