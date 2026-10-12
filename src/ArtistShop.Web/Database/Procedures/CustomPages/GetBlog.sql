DROP FUNCTION IF EXISTS get_blog_custom_page;

-- the page posts link up to
CREATE FUNCTION get_blog_custom_page () RETURNS TABLE (
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
    custom_pages
    LEFT JOIN home_page ON home_page.page_id = custom_pages.id
    LEFT JOIN blog_page ON blog_page.page_id = custom_pages.id
WHERE
    blog_page.page_id IS NOT NULL;
$$;
