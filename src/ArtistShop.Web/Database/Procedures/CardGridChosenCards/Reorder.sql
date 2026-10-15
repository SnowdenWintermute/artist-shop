DROP FUNCTION IF EXISTS reorder_card_grid_chosen_cards;

-- p_card_ids is every card in the grid's custom list, in the new order
CREATE FUNCTION reorder_card_grid_chosen_cards (p_component_id int, p_card_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    PERFORM lock_card_grid_chosen_cards(p_component_id);

    -- exactly the list's cards, or another tab changed them; the second count also rules out a repeat
    IF (
        SELECT
            COUNT(*) <> cardinality(p_card_ids)
            OR COUNT(*) FILTER (
                WHERE
                    chosen.id = ANY (p_card_ids)
            ) <> cardinality(p_card_ids)
        FROM
            card_grid_chosen_cards AS chosen
        WHERE
            chosen.component_id = p_component_id
    ) THEN
        RAISE EXCEPTION 'The custom list has changed since the page loaded.' USING ERRCODE = 'SH028';
    END IF;

    -- one statement, and the sort order constraint is DEFERRABLE, so two cards can swap places
    UPDATE card_grid_chosen_cards AS chosen
    SET
        sort_order = ordered.position - 1
    FROM
        unnest(p_card_ids) WITH ORDINALITY AS ordered (id, position)
    WHERE
        chosen.component_id = p_component_id
        AND chosen.id = ordered.id;
END;
$$;
