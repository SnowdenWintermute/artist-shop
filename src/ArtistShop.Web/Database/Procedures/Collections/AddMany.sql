DROP FUNCTION IF EXISTS add_many_collections;

-- a name or slug another collection has fails unique_collections_name or unique_collections_slug
CREATE FUNCTION add_many_collections (p_collections collection_name_and_slug[]) RETURNS TABLE (id int, name text) LANGUAGE sql AS $$
-- locked as in add_collection
LOCK TABLE collections IN SHARE ROW EXCLUSIVE MODE;

-- New collections go last. Every row of one INSERT reads the same MAX, so row_number spreads them out
-- rather than all of them asking for MAX + 1
INSERT INTO
    collections (name, slug, sort_order)
SELECT
    new_collection.name,
    new_collection.slug,
    (
        SELECT
            COALESCE(MAX(existing.sort_order), -1)
        FROM
            collections AS existing
    ) + row_number() OVER (
        ORDER BY
            new_collection.name
    )
    -- unnest over an array of a composite type gives one row per element and a column per field
FROM
    unnest(p_collections) AS new_collection
RETURNING
    id,
    name;
$$;
