DROP FUNCTION IF EXISTS add_collection;

-- a name or slug another collection has fails unique_collections_name or unique_collections_slug: unlike
-- works, collection slugs aren't numbered
CREATE FUNCTION add_collection (p_name text, p_slug text) RETURNS int LANGUAGE sql AS $$
-- New collections go last. Postgres won't lock rows under an aggregate like MAX, so this locks the
-- table instead: SHARE ROW EXCLUSIVE lets reads through but makes a second add (or a reorder) wait
-- until this one commits, rather than reading the same MAX and failing unique_collections_sort_order
LOCK TABLE collections IN SHARE ROW EXCLUSIVE MODE;

INSERT INTO
    collections (name, slug, sort_order)
SELECT
    p_name,
    p_slug,
    COALESCE(MAX(existing.sort_order), -1) + 1
FROM
    collections AS existing
RETURNING
    id;
$$;
