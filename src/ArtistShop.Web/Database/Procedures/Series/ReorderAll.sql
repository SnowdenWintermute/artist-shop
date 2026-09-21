DROP FUNCTION IF EXISTS reorder_series;

-- p_series_ids is every series, in the new order
CREATE FUNCTION reorder_series (p_series_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- a function can't change its own isolation level, so where the procedure ran SERIALIZABLE this
    -- locks the table: no series can be added or deleted between the check and the update
    LOCK TABLE series IN SHARE ROW EXCLUSIVE MODE;

    -- the list must be exactly the series that exist, or another tab added or deleted one after
    -- this page loaded. An array has no primary key to rule out a repeated id, so the second count
    -- is of the distinct series it matched: both equal to the list's length means the same set.
    -- cardinality is an array's length, and 0 for an empty one
    IF cardinality(p_series_ids) <> (
        SELECT
            COUNT(*)
        FROM
            series
    )
    OR cardinality(p_series_ids) <> (
        SELECT
            COUNT(*)
        FROM
            series
        WHERE
            series.id = ANY (p_series_ids)
    ) THEN
        RAISE EXCEPTION 'The series have changed since the page loaded.' USING ERRCODE = 'SH008';
    END IF;

    -- WITH ORDINALITY numbers each element by its place in the array, from 1.
    -- unique_series_sort_order is DEFERRABLE, so two series can swap places in this one statement
    UPDATE series
    SET
        sort_order = ordered.position - 1
    FROM
        unnest(p_series_ids) WITH ORDINALITY AS ordered (id, position)
    WHERE
        series.id = ordered.id;
END;
$$;
