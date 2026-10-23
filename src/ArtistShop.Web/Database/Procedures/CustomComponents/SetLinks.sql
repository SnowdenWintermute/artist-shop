DROP FUNCTION IF EXISTS set_custom_component_links;

-- Rebuilds a component's link row from its settings' linkTarget: a page, work, collection or post. An
-- address, or no link, has none, and so does a target deleted meanwhile, which then links nowhere
CREATE FUNCTION set_custom_component_links (p_component_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM custom_component_links
    WHERE
        component_id = p_component_id;

    INSERT INTO
        custom_component_links (component_id, page_id, work_id, collection_id, post_id)
    SELECT
        component.id,
        page.id,
        work.id,
        collection.id,
        post.id
    FROM
        custom_components AS component
        LEFT JOIN custom_pages AS page ON component.settings #>> '{linkTarget,type}' = 'Page'
        AND page.id = (component.settings #>> '{linkTarget,pageId}')::int
        LEFT JOIN works AS work ON component.settings #>> '{linkTarget,type}' = 'Work'
        AND work.id = (component.settings #>> '{linkTarget,workId}')::int
        LEFT JOIN collections AS collection ON component.settings #>> '{linkTarget,type}' = 'Collection'
        AND collection.id = (component.settings #>> '{linkTarget,collectionId}')::int
        LEFT JOIN posts AS post ON component.settings #>> '{linkTarget,type}' = 'Post'
        AND post.id = (component.settings #>> '{linkTarget,postId}')::int
    WHERE
        component.id = p_component_id
        AND num_nonnulls(page.id, work.id, collection.id, post.id) = 1;
END;
$$;
