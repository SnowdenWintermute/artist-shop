DROP FUNCTION IF EXISTS set_custom_component_works;

-- Rebuilds a text component's junction rows from its text, as set_post_works does a post's. Any other type
-- has no "body", so it has none. plpgsql, so rich_text_work_ids is looked up when it runs and the
-- files can run in any order
CREATE FUNCTION set_custom_component_works (p_component_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM custom_component_and_works_junction
    WHERE
        component_id = p_component_id;

    INSERT INTO
        custom_component_and_works_junction (component_id, work_id)
    SELECT DISTINCT
        p_component_id,
        work.id
    FROM
        custom_components AS component
        CROSS JOIN LATERAL rich_text_work_ids(component.settings -> 'body') AS embedded (work_id)
        JOIN works AS work ON work.id = embedded.work_id
    WHERE
        component.id = p_component_id;
END;
$$;
