DROP FUNCTION IF EXISTS set_custom_component_works;

-- Rebuilds an article's junction rows from its text, read as set_post_works reads a post's: an
-- article's settings hold the editor's Delta under "body". Any other type embeds nothing, so it has none
CREATE FUNCTION set_custom_component_works (p_component_id int) RETURNS void LANGUAGE sql AS $$
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
    CROSS JOIN LATERAL jsonb_path_query(
        component.settings,
        '$.body.ops[*].insert."artshop-work".workId ? (@.type() == "number" && @ == @.floor() && @ >= -2147483648 && @ <= 2147483647)'
    ) AS embedded (work_id)
    JOIN works AS work ON work.id = embedded.work_id::int
WHERE
    component.id = p_component_id;
$$;
