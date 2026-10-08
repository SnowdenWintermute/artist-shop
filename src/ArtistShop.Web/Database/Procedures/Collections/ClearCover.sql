DROP FUNCTION IF EXISTS clear_collection_cover;

-- with no starred work, the cover falls back to the first one in order with an image
CREATE FUNCTION clear_collection_cover (p_collection_id int) RETURNS void LANGUAGE sql AS $$
UPDATE work_and_collection_junction
SET
    is_cover = false
WHERE
    collection_id = p_collection_id
    AND is_cover;
$$;
