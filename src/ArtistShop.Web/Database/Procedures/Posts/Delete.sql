DROP FUNCTION IF EXISTS delete_post;

-- the junction rows go with it, by the cascade, and links to it are taken from the components that have
-- them first. plpgsql, so the function it calls is looked up when it runs and the files can run in any order
CREATE FUNCTION delete_post (p_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    PERFORM drop_custom_component_links_to('{}', '{}', '{}', ARRAY[p_id]);

    DELETE FROM posts
    WHERE
        id = p_id;
END;
$$;
