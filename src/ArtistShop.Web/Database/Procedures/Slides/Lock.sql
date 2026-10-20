DROP FUNCTION IF EXISTS lock_slides;

-- The owner's row is the lock for its slides' order, so two appends can't read the same MAX(sort_order),
-- and an append waits for a reorder and the reverse. The owner is a carousel component or a page row, the
-- other id null. Returns the last place taken, -1 for none. A component deleted meanwhile, or one that isn't
-- a carousel, which an address edited by hand could name, is SH026; a removed row is SH024
CREATE FUNCTION lock_slides (p_component_id int, p_row_id int) RETURNS int LANGUAGE plpgsql AS $$
DECLARE
    last_place int;
BEGIN
    IF p_component_id IS NOT NULL THEN
        PERFORM
        FROM
            custom_components
        WHERE
            custom_components.id = p_component_id
            -- CustomComponentType.Carousel
            AND custom_components.type = 5
        FOR NO KEY UPDATE;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'The component no longer exists.' USING ERRCODE = 'SH026';
        END IF;
    ELSE
        PERFORM
        FROM
            page_rows
        WHERE
            page_rows.id = p_row_id
        FOR NO KEY UPDATE;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'The row has changed since the page loaded.' USING ERRCODE = 'SH024';
        END IF;
    END IF;

    -- MAX over no rows is NULL
    SELECT
        COALESCE(MAX(slides.sort_order), -1)
    INTO
        last_place
    FROM
        slides
    WHERE
        slides.component_id = p_component_id
        OR slides.row_id = p_row_id;

    RETURN last_place;
END;
$$;
