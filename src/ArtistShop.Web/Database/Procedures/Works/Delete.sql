-- the single-work function this replaced, left in databases made before it
DROP FUNCTION IF EXISTS delete_work;

DROP FUNCTION IF EXISTS delete_works;

-- Every table that references a work cascades, so this one statement also removes their images,
-- their collection and vocabulary term rows and their products. The image files stay until
-- OrphanedImageSweeper finds no row pointing at them, which is the same path an abandoned upload
-- takes. An id already deleted is passed over
CREATE FUNCTION delete_works (p_ids int[]) RETURNS void LANGUAGE sql AS $$
DELETE FROM works
WHERE
    id = ANY (p_ids);
$$;
