DROP FUNCTION IF EXISTS lock_card_grid_chosen_cards;

-- The component row is the lock for its custom list's order, so two appends can't read the same
-- MAX(sort_order), and an append waits for a reorder and the reverse. Returns the last place taken, -1 for
-- an empty list. A component deleted meanwhile is SH026, as is one that isn't a card grid, which an address
-- edited by hand could name
CREATE FUNCTION lock_card_grid_chosen_cards (p_component_id int) RETURNS int LANGUAGE plpgsql AS $$
DECLARE
    last_place int;
BEGIN
    PERFORM
    FROM
        custom_components
    WHERE
        custom_components.id = p_component_id
        -- CustomComponentType.CardGrid
        AND custom_components.type = 1
    FOR NO KEY UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The component no longer exists.' USING ERRCODE = 'SH026';
    END IF;

    -- MAX over no rows is NULL
    SELECT
        COALESCE(MAX(chosen.sort_order), -1)
    INTO
        last_place
    FROM
        card_grid_chosen_cards AS chosen
    WHERE
        chosen.component_id = p_component_id;

    RETURN last_place;
END;
$$;
