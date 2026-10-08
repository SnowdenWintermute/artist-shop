DROP FUNCTION IF EXISTS set_work_collections;

-- A collection row does carry something of its own: sort_order and is_cover. So unticked rows are
-- deleted and newly ticked ones appended, leaving the rest where the artist dragged them. Dropping a
-- row takes is_cover with it, which is what removing a collection's cover work from that collection means.
CREATE FUNCTION set_work_collections (p_work_id int, p_collection_ids int[]) RETURNS void LANGUAGE sql AS $$
-- Appending reads MAX(sort_order), and Postgres won't lock rows under an aggregate, so each chosen
-- collection row is the lock for its own works' order: a second append waits here rather than reading
-- the same MAX, and reorder_collection_works takes the same lock. NO KEY UPDATE lets foreign key checks through, and id order
-- means two callers can't each hold a collection the other wants
SELECT
FROM
    collections
WHERE
    id = ANY (p_collection_ids)
ORDER BY
    id
FOR NO KEY UPDATE;

DELETE FROM work_and_collection_junction
WHERE
    work_id = p_work_id
    AND NOT collection_id = ANY (p_collection_ids);

INSERT INTO
    work_and_collection_junction (work_id, collection_id, sort_order)
SELECT
    p_work_id,
    chosen.id,
    -- a correlated subquery: it runs once per chosen collection. MAX over no rows is NULL, so
    -- COALESCE turns "empty collection" into -1, which the + 1 makes 0
    COALESCE(
        (
            SELECT
                MAX(existing.sort_order)
            FROM
                work_and_collection_junction AS existing
            WHERE
                existing.collection_id = chosen.id
        ),
        -1
    ) + 1
FROM
    unnest(p_collection_ids) AS chosen (id)
    -- a collection the work is already in keeps its place
ON CONFLICT (work_id, collection_id) DO NOTHING;
$$;
