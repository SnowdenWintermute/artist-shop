DROP FUNCTION IF EXISTS get_post_list;

-- Drafts first for the artist, since those are the ones being worked on, then newest published.
-- Postgres sorts NULL as larger than any value, so DESC already puts drafts first; NULLS FIRST
-- says so rather than leaving it to be known
CREATE FUNCTION get_post_list () RETURNS TABLE (
    id int,
    title text,
    slug text,
    published_at timestamptz,
    updated_at timestamptz
) LANGUAGE sql STABLE AS $$
SELECT
    post.id,
    post.title,
    post.slug,
    post.published_at,
    post.updated_at
FROM
    posts AS post
ORDER BY
    post.published_at DESC NULLS FIRST,
    post.id DESC;
$$;
