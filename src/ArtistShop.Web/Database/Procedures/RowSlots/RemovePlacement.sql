DROP FUNCTION IF EXISTS remove_slot_placement;

-- the component stays in the library. One no longer in the slot does nothing
CREATE FUNCTION remove_slot_placement (p_slot_id int, p_component_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM slot_placements
    WHERE
        slot_id = p_slot_id
        AND component_id = p_component_id;

    PERFORM check_blog_page_has_post_list();
    PERFORM check_collections_page_has_grid();
END;
$$;
