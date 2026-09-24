DROP FUNCTION IF EXISTS get_published_post_page;

-- One page of the public blog list, newest first, with each post's body for its excerpt
CREATE FUNCTION get_published_post_page (p_offset int, p_page_size int) RETURNS TABLE (
    id int,
    title text,
    slug text,
    body jsonb,
    published_at timestamptz,
    total_count int
) LANGUAGE sql STABLE AS $$
SELECT
    post.id,
    post.title,
    post.slug,
    post.body,
    post.published_at,
    -- every published post's count, repeated on each row of this page
    (COUNT(*) OVER ())::int
FROM
    posts AS post
WHERE
    post.published_at <= now()
ORDER BY
    post.published_at DESC,
    post.id DESC
OFFSET
    p_offset
LIMIT
    p_page_size;
$$;
