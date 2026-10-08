DROP FUNCTION IF EXISTS reorder_collection_works;

-- p_work_ids is every work in the collection, in the new order
CREATE FUNCTION reorder_collection_works (p_collection_id int, p_work_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- in place of SERIALIZABLE. The collection row is the lock for its works' order, as in
    -- set_work_collections, so an append to this collection waits for the reorder and the reverse; other
    -- collections carry on. A removal takes no lock, but one landing between the check and the UPDATE only
    -- leaves a gap in sort_order, which remove_works_from_collection leaves anyway
    PERFORM
    FROM
        collections
    WHERE
        collections.id = p_collection_id
    FOR NO KEY UPDATE;

    -- the list must be exactly the collection's works, or another tab changed them after this page
    -- loaded. The second count is of distinct members matched, which also rules out a repeated id
    IF cardinality(p_work_ids) <> (
        SELECT
            COUNT(*)
        FROM
            work_and_collection_junction AS junction
        WHERE
            junction.collection_id = p_collection_id
    )
    OR cardinality(p_work_ids) <> (
        SELECT
            COUNT(*)
        FROM
            work_and_collection_junction AS junction
        WHERE
            junction.collection_id = p_collection_id
            AND junction.work_id = ANY (p_work_ids)
    ) THEN
        RAISE EXCEPTION 'The collection has changed since the page loaded.' USING ERRCODE = 'SH005';
    END IF;

    -- one statement, and the (collection_id, sort_order) constraint is DEFERRABLE, so two works
    -- can swap places. Row-by-row updates would collide halfway
    UPDATE work_and_collection_junction AS junction
    SET
        sort_order = ordered.position - 1
    FROM
        unnest(p_work_ids) WITH ORDINALITY AS ordered (id, position)
    WHERE
        junction.collection_id = p_collection_id
        AND junction.work_id = ordered.id;
END;
$$;
