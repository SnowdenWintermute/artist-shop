-- the single-work function this replaced, left in databases made before it
DROP FUNCTION IF EXISTS delete_work;

DROP FUNCTION IF EXISTS delete_works;

-- Every table that references a work cascades, so deleting them also removes their images, their
-- collection and vocabulary term rows and their products. The image components showing them are
-- deleted first, the row backgrounds showing them cleared, as page_rows doesn't cascade, and links to
-- them taken from the components that have them. The image files stay until OrphanedImageSweeper finds no row pointing at them, which
-- is the same path an abandoned upload takes. An id already deleted is passed over. plpgsql, so
-- the functions it calls are looked up when it runs and the files can run in any order
CREATE FUNCTION delete_works (p_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    PERFORM delete_image_components_showing(p_ids, '{}');
    PERFORM clear_row_backgrounds_showing(p_ids, '{}');
    PERFORM drop_custom_component_links_to('{}', p_ids, '{}', '{}');

    DELETE FROM works
    WHERE
        id = ANY (p_ids);
END;
$$;
