DROP FUNCTION IF EXISTS remove_works_from_collection;

-- leaves gaps in sort_order, which is fine: only the order matters, and a reorder renumbers
CREATE FUNCTION remove_works_from_collection (p_collection_id int, p_work_ids int[]) RETURNS void LANGUAGE sql AS $$
DELETE FROM work_and_collection_junction
WHERE
    collection_id = p_collection_id
    AND work_id = ANY (p_work_ids);
$$;
