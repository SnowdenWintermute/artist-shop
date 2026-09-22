DROP FUNCTION IF EXISTS add_series;

-- a name or slug another series has fails unique_series_name or unique_series_slug: unlike
-- artworks, series slugs aren't numbered
CREATE FUNCTION add_series (p_name text, p_slug text) RETURNS int LANGUAGE sql AS $$
-- New series go last. Postgres won't lock rows under an aggregate like MAX, so this locks the
-- table instead: SHARE ROW EXCLUSIVE lets reads through but makes a second add (or a reorder) wait
-- until this one commits, rather than reading the same MAX and failing unique_series_sort_order
LOCK TABLE series IN SHARE ROW EXCLUSIVE MODE;

INSERT INTO
    series (name, slug, sort_order)
SELECT
    p_name,
    p_slug,
    COALESCE(MAX(existing.sort_order), -1) + 1
FROM
    series AS existing
RETURNING
    id;
$$;
