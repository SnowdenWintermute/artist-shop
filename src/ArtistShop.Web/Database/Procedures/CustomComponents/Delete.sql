DROP FUNCTION IF EXISTS delete_custom_component;

-- Taken off every page it's on. The works it mentions stay; only the mentions go. Deleting one
-- that's gone already does nothing
CREATE FUNCTION delete_custom_component (p_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- FOR UPDATE holds off place_custom_component, whose placement would stop the delete, until
    -- this commits; it then fails the foreign key
    PERFORM 1 FROM custom_components WHERE id = p_id FOR UPDATE;

    DELETE FROM slot_placements
    WHERE
        component_id = p_id;

    DELETE FROM custom_components
    WHERE
        id = p_id;

    PERFORM check_blog_page_has_post_list();
END;
$$;
