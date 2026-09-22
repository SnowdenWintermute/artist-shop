DROP FUNCTION IF EXISTS get_post;

CREATE FUNCTION get_post (p_id int) RETURNS TABLE (
    id int,
    title text,
    slug text,
    body jsonb,
    published_at timestamptz,
    created_at timestamptz,
    updated_at timestamptz
) LANGUAGE sql STABLE AS $$
SELECT
    post.id,
    post.title,
    post.slug,
    post.body,
    post.published_at,
    post.created_at,
    post.updated_at
FROM
    posts AS post
WHERE
    post.id = p_id;
$$;
