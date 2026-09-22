DROP FUNCTION IF EXISTS get_published_post_by_slug;

-- a visitor's lookup: a draft answers exactly as a slug nobody has
CREATE FUNCTION get_published_post_by_slug (p_slug text) RETURNS TABLE (
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
    post.slug = p_slug
    AND post.published_at <= now();
$$;
