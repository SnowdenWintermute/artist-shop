DROP FUNCTION IF EXISTS set_custom_component_works;

-- Rebuilds a component's junction rows: a text component's from its text, as set_post_works does a post's,
-- and an image component's from a work source. Any other type has neither, so it has none. plpgsql, so rich_text_work_ids is looked up when it runs and the
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
        CROSS JOIN LATERAL (
            SELECT
                embedded.work_id
            FROM
                rich_text_work_ids(component.settings -> 'body') AS embedded (work_id)
            UNION ALL
            SELECT
                (component.settings #>> '{source,workId}')::int
            WHERE
                component.settings #>> '{source,type}' = 'Work'
        ) AS used (work_id)
        JOIN works AS work ON work.id = used.work_id
    WHERE
        component.id = p_component_id;
END;
$$;
