DROP FUNCTION IF EXISTS reorder_slides;

-- p_slide_ids is every one of the owner's slides, in the new order
CREATE FUNCTION reorder_slides (p_component_id int, p_row_id int, p_slide_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    PERFORM lock_slides(p_component_id, p_row_id);

    -- exactly the owner's slides, or another tab changed them; the second count also rules out a repeat
    IF (
        SELECT
            COUNT(*) <> cardinality(p_slide_ids)
            OR COUNT(*) FILTER (
                WHERE
                    slides.id = ANY (p_slide_ids)
            ) <> cardinality(p_slide_ids)
        FROM
            slides
        WHERE
            slides.component_id = p_component_id
            OR slides.row_id = p_row_id
    ) THEN
        RAISE EXCEPTION 'The slides have changed since the page loaded.' USING ERRCODE = 'SH030';
    END IF;

    -- one statement, and the sort order constraints are DEFERRABLE, so two slides can swap places
    UPDATE slides
    SET
        sort_order = ordered.position - 1
    FROM
        unnest(p_slide_ids) WITH ORDINALITY AS ordered (id, position)
    WHERE
        slides.id = ordered.id
        AND (
            slides.component_id = p_component_id
            OR slides.row_id = p_row_id
        );
END;
$$;
