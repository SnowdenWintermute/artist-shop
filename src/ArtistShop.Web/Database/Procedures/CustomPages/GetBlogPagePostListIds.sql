DROP FUNCTION IF EXISTS get_blog_page_post_list_ids;

-- the post list components on the blog page, which must always hold one (check_blog_page_has_post_list)
CREATE FUNCTION get_blog_page_post_list_ids () RETURNS TABLE (id int) LANGUAGE sql STABLE AS $$
SELECT DISTINCT
    custom_components.id
FROM
    blog_page
    JOIN page_rows ON page_rows.page_id = blog_page.page_id
    JOIN row_slots ON row_slots.row_id = page_rows.id
    JOIN slot_placements ON slot_placements.slot_id = row_slots.id
    JOIN custom_components ON custom_components.id = slot_placements.component_id
WHERE
    -- CustomComponentType.PostList
    custom_components.type = 2;
$$;
