DROP FUNCTION IF EXISTS delete_series;

CREATE FUNCTION delete_series (p_id int) RETURNS void LANGUAGE sql AS $$
DELETE FROM artwork_and_series_junction
WHERE
    series_id = p_id;

DELETE FROM series
WHERE
    id = p_id;
$$;
