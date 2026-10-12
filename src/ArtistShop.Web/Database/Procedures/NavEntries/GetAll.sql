DROP FUNCTION IF EXISTS get_all_nav_entries;

-- the pages in the nav, in the artist's order
CREATE FUNCTION get_all_nav_entries () RETURNS TABLE (
    id int,
    name text,
    address text,
    is_home boolean,
    is_blog boolean
) LANGUAGE sql STABLE AS $$
SELECT
    custom_pages.id,
    custom_pages.name,
    custom_pages.address,
    home_page.page_id IS NOT NULL,
    blog_page.page_id IS NOT NULL
FROM
    nav_entries
    JOIN custom_pages ON custom_pages.id = nav_entries.page_id
    LEFT JOIN home_page ON home_page.page_id = custom_pages.id
    LEFT JOIN blog_page ON blog_page.page_id = custom_pages.id
ORDER BY
    nav_entries.sort_order;
$$;
