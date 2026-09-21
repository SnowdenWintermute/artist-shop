DROP FUNCTION IF EXISTS set_artwork_series;

-- A series row does carry something of its own: sort_order and is_cover. So unticked rows are
-- deleted and newly ticked ones appended, leaving the rest where the artist dragged them. Dropping a
-- row takes is_cover with it, which is what removing a series' cover artwork from that series means.
CREATE FUNCTION set_artwork_series (p_artwork_id int, p_series_ids int[]) RETURNS void LANGUAGE sql AS $$
-- Appending reads MAX(sort_order), and Postgres won't lock rows under an aggregate, so each chosen
-- series row is the lock for its own artworks' order: a second append (or a reorder) waits here
-- rather than reading the same MAX. NO KEY UPDATE lets foreign key checks through, and id order
-- means two callers can't each hold a series the other wants
SELECT
FROM
    series
WHERE
    id = ANY (p_series_ids)
ORDER BY
    id
FOR NO KEY UPDATE;

DELETE FROM artwork_and_series_junction
WHERE
    artwork_id = p_artwork_id
    AND NOT series_id = ANY (p_series_ids);

INSERT INTO
    artwork_and_series_junction (artwork_id, series_id, sort_order)
SELECT
    p_artwork_id,
    chosen.id,
    -- a correlated subquery: it runs once per chosen series. MAX over no rows is NULL, so
    -- COALESCE turns "empty series" into -1, which the + 1 makes 0
    COALESCE(
        (
            SELECT
                MAX(existing.sort_order)
            FROM
                artwork_and_series_junction AS existing
            WHERE
                existing.series_id = chosen.id
        ),
        -1
    ) + 1
FROM
    unnest(p_series_ids) AS chosen (id)
    -- a series the artwork is already in keeps its place
ON CONFLICT (artwork_id, series_id) DO NOTHING;
$$;
