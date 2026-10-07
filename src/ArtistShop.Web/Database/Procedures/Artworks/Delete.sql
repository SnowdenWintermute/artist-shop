-- the single-artwork function this replaced, left in databases made before it
DROP FUNCTION IF EXISTS delete_artwork;

DROP FUNCTION IF EXISTS delete_artworks;

-- Every table that references an artwork cascades, so this one statement also removes their images,
-- their series and vocabulary term rows and their products. The image files stay until
-- OrphanedImageSweeper finds no row pointing at them, which is the same path an abandoned upload
-- takes. An id already deleted is passed over
CREATE FUNCTION delete_artworks (p_ids int[]) RETURNS void LANGUAGE sql AS $$
DELETE FROM artworks
WHERE
    id = ANY (p_ids);
$$;
