DROP FUNCTION IF EXISTS get_custom_pages_mentioning_work;

-- the pages with an article on them that embeds the work. Every custom page is public
CREATE FUNCTION get_custom_pages_mentioning_work (p_work_id int) RETURNS TABLE (
    id int,
    name text,
    address text,
    is_home boolean,
    is_blog boolean
) LANGUAGE sql STABLE AS $$
SELECT DISTINCT
    custom_pages.id,
    custom_pages.name,
    custom_pages.address,
    home_page.page_id IS NOT NULL,
    blog_page.page_id IS NOT NULL
FROM
    custom_component_and_works_junction AS junction
    JOIN slot_placements ON slot_placements.component_id = junction.component_id
    JOIN row_slots ON row_slots.id = slot_placements.slot_id
    JOIN page_rows ON page_rows.id = row_slots.row_id
    JOIN custom_pages ON custom_pages.id = page_rows.page_id
    LEFT JOIN home_page ON home_page.page_id = custom_pages.id
    LEFT JOIN blog_page ON blog_page.page_id = custom_pages.id
WHERE
    junction.work_id = p_work_id;
$$;
