DROP FUNCTION IF EXISTS drop_custom_component_links_to;

-- Takes the link from every component linking to any of these pages, works, collections or posts, as
-- they're deleted: a button, which is nothing without one, is deleted, taking it off every page it's on; a
-- heading's link goes, and an image goes back to no link (Mike, 2026-10-23). Called before the targets go,
-- while the link rows still say which
CREATE FUNCTION drop_custom_component_links_to (p_page_ids int[], p_work_ids int[], p_collection_ids int[], p_post_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    v_button_ids int[];
BEGIN
    SELECT
        array_agg(component.id) INTO v_button_ids
    FROM
        custom_components AS component
        JOIN custom_component_links AS link ON link.component_id = component.id
    WHERE
        -- CustomComponentType.Button
        component.type = 7
        AND (
            link.page_id = ANY (p_page_ids)
            OR link.work_id = ANY (p_work_ids)
            OR link.collection_id = ANY (p_collection_ids)
            OR link.post_id = ANY (p_post_ids)
        );

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
    FROM
        custom_component_links AS link
    WHERE
        link.component_id = component.id
        AND (
            link.page_id = ANY (p_page_ids)
            OR link.work_id = ANY (p_work_ids)
            OR link.collection_id = ANY (p_collection_ids)
            OR link.post_id = ANY (p_post_ids)
        );

    DELETE FROM custom_component_links AS link
    WHERE
        link.page_id = ANY (p_page_ids)
        OR link.work_id = ANY (p_work_ids)
        OR link.collection_id = ANY (p_collection_ids)
        OR link.post_id = ANY (p_post_ids);
END;
$$;
