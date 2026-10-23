DROP FUNCTION IF EXISTS drop_custom_component_links_to;

-- Takes the link from every component linking to any of these pages, works, collections or posts, as
-- they're deleted: a button, which is nothing without one, is deleted, taking it off every page it's on; a
-- heading's link goes, and an image goes back to no link (Mike, 2026-10-23). Called before the targets go,
-- while the link rows still say which. plpgsql, so get_custom_components_linking_to is looked up when it
-- runs and the files can run in any order
CREATE FUNCTION drop_custom_component_links_to (p_page_ids int[], p_work_ids int[], p_collection_ids int[], p_post_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    v_button_ids int[];
    v_other_ids int[];
BEGIN
    SELECT
        -- CustomComponentType.Button
        array_agg(linking.id) FILTER (WHERE linking.type = 7),
        array_agg(linking.id) FILTER (WHERE linking.type <> 7) INTO v_button_ids, v_other_ids
    FROM
        get_custom_components_linking_to(p_page_ids, p_work_ids, p_collection_ids, p_post_ids) AS linking;

    DELETE FROM slot_placements
    WHERE
        component_id = ANY (v_button_ids);

    DELETE FROM custom_components
    WHERE
        id = ANY (v_button_ids);

    UPDATE custom_components AS component
    SET
        settings = CASE
            -- CustomComponentType.Image, whose link is a choice of its own
            WHEN component.type = 4 THEN (component.settings - 'linkTarget') || '{"link": "None"}'
            ELSE component.settings - 'linkTarget'
        END,
        updated_at = now()
    WHERE
        component.id = ANY (v_other_ids);

    DELETE FROM custom_component_links
    WHERE
        component_id = ANY (v_other_ids);
END;
$$;
