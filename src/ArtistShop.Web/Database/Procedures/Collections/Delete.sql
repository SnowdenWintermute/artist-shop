DROP FUNCTION IF EXISTS delete_collection;

CREATE FUNCTION delete_collection (p_id int) RETURNS void LANGUAGE sql AS $$
DELETE FROM work_and_collection_junction
WHERE
    collection_id = p_id;

DELETE FROM collections
WHERE
    id = p_id;
$$;
