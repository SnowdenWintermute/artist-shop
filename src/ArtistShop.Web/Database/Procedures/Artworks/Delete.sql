DROP FUNCTION IF EXISTS delete_artwork;

-- Every table that references an artwork cascades, so this one statement also removes its images,
-- its series and vocabulary term rows and its products. The image files stay until
-- OrphanedImageSweeper finds no row pointing at them, which is the same path an abandoned upload
-- takes.
CREATE FUNCTION delete_artwork (p_id int) RETURNS void LANGUAGE sql AS $$
DELETE FROM artworks
WHERE
    id = p_id;
$$;
