DROP FUNCTION IF EXISTS delete_artworks;

-- delete_artwork for several at once, as the artwork table's bulk delete. An id already deleted is
-- passed over
CREATE FUNCTION delete_artworks (p_ids int[]) RETURNS void LANGUAGE sql AS $$
DELETE FROM artworks
WHERE
    id = ANY (p_ids);
$$;
