DROP FUNCTION IF EXISTS remove_artworks_from_series;

-- leaves gaps in sort_order, which is fine: only the order matters, and a reorder renumbers
CREATE FUNCTION remove_artworks_from_series (p_series_id int, p_artwork_ids int[]) RETURNS void LANGUAGE sql AS $$
DELETE FROM artwork_and_series_junction
WHERE
    series_id = p_series_id
    AND artwork_id = ANY (p_artwork_ids);
$$;
