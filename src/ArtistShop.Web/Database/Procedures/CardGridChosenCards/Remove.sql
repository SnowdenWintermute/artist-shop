DROP FUNCTION IF EXISTS remove_card_grid_chosen_card;

-- Takes one card off the custom list; the collection or work stays. Leaves a gap in sort_order, which is
-- fine: only the order matters, and a reorder renumbers. Removing one that's gone already does nothing
CREATE FUNCTION remove_card_grid_chosen_card (p_component_id int, p_card_id int) RETURNS void LANGUAGE sql AS $$
DELETE FROM card_grid_chosen_cards
WHERE
    component_id = p_component_id
    AND id = p_card_id;
$$;
