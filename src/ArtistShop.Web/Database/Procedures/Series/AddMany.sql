DROP FUNCTION IF EXISTS add_many_series;

-- a name or slug another series has fails unique_series_name or unique_series_slug
CREATE FUNCTION add_many_series (p_series series_name_and_slug[]) RETURNS TABLE (id int, name text) LANGUAGE sql AS $$
-- locked as in add_series
LOCK TABLE series IN SHARE ROW EXCLUSIVE MODE;

-- New series go last. Every row of one INSERT reads the same MAX, so row_number spreads them out
-- rather than all of them asking for MAX + 1
INSERT INTO
    series (name, slug, sort_order)
SELECT
    new_series.name,
    new_series.slug,
    (
        SELECT
            COALESCE(MAX(existing.sort_order), -1)
        FROM
            series AS existing
    ) + row_number() OVER (
        ORDER BY
            new_series.name
    )
    -- unnest over an array of a composite type gives one row per element and a column per field
FROM
    unnest(p_series) AS new_series
RETURNING
    id,
    name;
$$;
