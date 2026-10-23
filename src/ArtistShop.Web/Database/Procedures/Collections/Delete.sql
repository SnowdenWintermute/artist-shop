DROP FUNCTION IF EXISTS delete_collection;

-- links to it are taken from the components that have them first. plpgsql, so the function it calls is
-- looked up when it runs and the files can run in any order
CREATE FUNCTION delete_collection (p_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    PERFORM drop_custom_component_links_to('{}', '{}', ARRAY[p_id], '{}');

    DELETE FROM work_and_collection_junction
    WHERE
        collection_id = p_id;

    DELETE FROM collections
    WHERE
        id = p_id;
END;
$$;
