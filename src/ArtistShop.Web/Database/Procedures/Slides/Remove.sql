DROP FUNCTION IF EXISTS remove_slide;

-- Takes one slide off its owner's list; the image stays. Leaves a gap in sort_order, which is fine: only the
-- order matters, and a reorder renumbers. Removing one that's gone already does nothing
CREATE FUNCTION remove_slide (p_component_id int, p_row_id int, p_slide_id int) RETURNS void LANGUAGE sql AS $$
DELETE FROM slides
WHERE
    id = p_slide_id
    AND (
        component_id = p_component_id
        OR row_id = p_row_id
    );
$$;
