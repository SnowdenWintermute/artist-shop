DROP FUNCTION IF EXISTS add_works_to_collection;

-- the works join the end of the collection, in the order given. One already in it keeps its place,
-- and one deleted since the page loaded is left out
CREATE FUNCTION add_works_to_collection (p_collection_id int, p_work_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    last_place int;
BEGIN
    -- the collection row is the lock for its works' order, as in set_work_collections, so two appends
    -- can't read the same MAX(sort_order)
    PERFORM
    FROM
        collections
    WHERE
        collections.id = p_collection_id
    FOR NO KEY UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The collection no longer exists.' USING ERRCODE = 'SH004';
    END IF;

    -- MAX over no rows is NULL, so an empty collection starts at 0
    SELECT
        COALESCE(MAX(junction.sort_order), -1)
    INTO
        last_place
    FROM
        work_and_collection_junction AS junction
    WHERE
        junction.collection_id = p_collection_id;

    -- gaps where a work is skipped are fine: only the order matters, and a reorder renumbers
    INSERT INTO
        work_and_collection_junction (work_id, collection_id, sort_order)
    SELECT
        work.id,
        p_collection_id,
        last_place + chosen.position
    FROM
        unnest(p_work_ids) WITH ORDINALITY AS chosen (id, position)
        JOIN works AS work ON work.id = chosen.id
    ON CONFLICT (work_id, collection_id) DO NOTHING;
END;
$$;
