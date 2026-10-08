DROP FUNCTION IF EXISTS reorder_collections;

-- p_collection_ids is every collection, in the new order
CREATE FUNCTION reorder_collections (p_collection_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- a function can't change its own isolation level, so where the procedure ran SERIALIZABLE this
    -- locks the table: no collection can be added or deleted between the check and the update
    LOCK TABLE collections IN SHARE ROW EXCLUSIVE MODE;

    -- the list must be exactly the collections that exist, or another tab added or deleted one after
    -- this page loaded. An array has no primary key to rule out a repeated id, so the second count
    -- is of the distinct collections it matched: both equal to the list's length means the same set.
    -- cardinality is an array's length, and 0 for an empty one
    IF cardinality(p_collection_ids) <> (
        SELECT
            COUNT(*)
        FROM
            collections
    )
    OR cardinality(p_collection_ids) <> (
        SELECT
            COUNT(*)
        FROM
            collections
        WHERE
            collections.id = ANY (p_collection_ids)
    ) THEN
        RAISE EXCEPTION 'The collections have changed since the page loaded.' USING ERRCODE = 'SH008';
    END IF;

    -- WITH ORDINALITY numbers each element by its place in the array, from 1.
    -- unique_collections_sort_order is DEFERRABLE, so two collections can swap places in this one statement
    UPDATE collections
    SET
        sort_order = ordered.position - 1
    FROM
        unnest(p_collection_ids) WITH ORDINALITY AS ordered (id, position)
    WHERE
        collections.id = ordered.id;
END;
$$;
