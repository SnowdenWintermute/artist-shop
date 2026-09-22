DROP FUNCTION IF EXISTS clear_series_cover;

-- with no starred artwork, the cover falls back to the first one in order with an image
CREATE FUNCTION clear_series_cover (p_series_id int) RETURNS void LANGUAGE sql AS $$
UPDATE artwork_and_series_junction
SET
    is_cover = false
WHERE
    series_id = p_series_id
    AND is_cover;
$$;
