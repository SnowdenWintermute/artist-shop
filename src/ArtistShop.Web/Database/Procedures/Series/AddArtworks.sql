DROP FUNCTION IF EXISTS add_artworks_to_series;

-- the artworks join the end of the series, in the order given. One already in it keeps its place,
-- and one deleted since the page loaded is left out
CREATE FUNCTION add_artworks_to_series (p_series_id int, p_artwork_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    last_place int;
BEGIN
    -- the series row is the lock for its artworks' order, as in set_artwork_series, so two appends
    -- can't read the same MAX(sort_order)
    PERFORM
    FROM
        series
    WHERE
        series.id = p_series_id
    FOR NO KEY UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The series no longer exists.' USING ERRCODE = 'SH004';
    END IF;

    -- MAX over no rows is NULL, so an empty series starts at 0
    SELECT
        COALESCE(MAX(junction.sort_order), -1)
    INTO
        last_place
    FROM
        artwork_and_series_junction AS junction
    WHERE
        junction.series_id = p_series_id;

    -- gaps where an artwork is skipped are fine: only the order matters, and a reorder renumbers
    INSERT INTO
        artwork_and_series_junction (artwork_id, series_id, sort_order)
    SELECT
        artwork.id,
        p_series_id,
        last_place + chosen.position
    FROM
        unnest(p_artwork_ids) WITH ORDINALITY AS chosen (id, position)
        JOIN artworks AS artwork ON artwork.id = chosen.id
    ON CONFLICT (artwork_id, series_id) DO NOTHING;
END;
$$;
