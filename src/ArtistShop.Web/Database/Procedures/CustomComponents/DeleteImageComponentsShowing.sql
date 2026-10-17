DROP FUNCTION IF EXISTS delete_image_components_showing;

-- Deletes the image components showing any of these works or library images, taking them off every page
-- they're on, as the works or images are deleted: a picture gone for good leaves nothing to show (Mike,
-- 2026-10-16). Called before the works or images go, while their junction rows still say which. plpgsql,
-- so get_image_components_showing is looked up when it runs and the files can run in any order
CREATE FUNCTION delete_image_components_showing (p_work_ids int[], p_storage_keys text[]) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    v_ids int[];
BEGIN
    SELECT
        array_agg(showing.id) INTO v_ids
    FROM
        get_image_components_showing(p_work_ids, p_storage_keys) AS showing;

    DELETE FROM slot_placements
    WHERE
        component_id = ANY (v_ids);

    DELETE FROM custom_components
    WHERE
        id = ANY (v_ids);
END;
$$;
