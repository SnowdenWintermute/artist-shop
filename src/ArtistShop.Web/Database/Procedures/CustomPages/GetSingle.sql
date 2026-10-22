DROP FUNCTION IF EXISTS get_custom_page;

CREATE FUNCTION get_custom_page (p_id int) RETURNS TABLE (
    id int,
    name text,
    address text,
    is_home boolean,
    is_blog boolean,
    is_collections boolean
) LANGUAGE sql STABLE AS $$
SELECT
    custom_pages.id,
    custom_pages.name,
    custom_pages.address,
    home_page.page_id IS NOT NULL,
    blog_page.page_id IS NOT NULL,
    collections_page.page_id IS NOT NULL
FROM
    custom_pages
    LEFT JOIN home_page ON home_page.page_id = custom_pages.id
    LEFT JOIN blog_page ON blog_page.page_id = custom_pages.id
    LEFT JOIN collections_page ON collections_page.page_id = custom_pages.id
WHERE
    custom_pages.id = p_id;
$$;
