DROP FUNCTION IF EXISTS reorder_series_artworks;

-- p_artwork_ids is every artwork in the series, in the new order
CREATE FUNCTION reorder_series_artworks (p_series_id int, p_artwork_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- in place of SERIALIZABLE. The series row is the lock for its artworks' order, as in
    -- set_artwork_series, so an append to this series waits for the reorder and the reverse; other
    -- series carry on. A removal takes no lock, but one landing between the check and the UPDATE only
    -- leaves a gap in sort_order, which remove_artworks_from_series leaves anyway
    PERFORM
    FROM
        series
    WHERE
        series.id = p_series_id
    FOR NO KEY UPDATE;

    -- the list must be exactly the series' artworks, or another tab changed them after this page
    -- loaded. The second count is of distinct members matched, which also rules out a repeated id
    IF cardinality(p_artwork_ids) <> (
        SELECT
            COUNT(*)
        FROM
            artwork_and_series_junction AS junction
        WHERE
            junction.series_id = p_series_id
    )
    OR cardinality(p_artwork_ids) <> (
        SELECT
            COUNT(*)
        FROM
            artwork_and_series_junction AS junction
        WHERE
            junction.series_id = p_series_id
            AND junction.artwork_id = ANY (p_artwork_ids)
    ) THEN
        RAISE EXCEPTION 'The series has changed since the page loaded.' USING ERRCODE = 'SH005';
    END IF;

    -- one statement, and the (series_id, sort_order) constraint is DEFERRABLE, so two artworks
    -- can swap places. Row-by-row updates would collide halfway
    UPDATE artwork_and_series_junction AS junction
    SET
        sort_order = ordered.position - 1
    FROM
        unnest(p_artwork_ids) WITH ORDINALITY AS ordered (id, position)
    WHERE
        junction.series_id = p_series_id
        AND junction.artwork_id = ordered.id;
END;
$$;
