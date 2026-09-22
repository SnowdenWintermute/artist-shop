DROP FUNCTION IF EXISTS get_post_by_slug;

-- an admin's lookup, drafts included, so the artist can read a draft on the page it will appear on
CREATE FUNCTION get_post_by_slug (p_slug text) RETURNS TABLE (
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
    post.slug = p_slug;
$$;
