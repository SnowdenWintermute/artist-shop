DROP FUNCTION IF EXISTS get_published_posts_mentioning_artwork;

CREATE FUNCTION get_published_posts_mentioning_artwork (p_artwork_id int) RETURNS TABLE (
    id int,
    title text,
    slug text,
    published_at timestamptz
) LANGUAGE sql STABLE AS $$
SELECT
    post.id,
    post.title,
    post.slug,
    post.published_at
FROM
    post_and_artworks_junction AS junction
    JOIN posts AS post ON post.id = junction.post_id
WHERE
    junction.artwork_id = p_artwork_id
    AND post.published_at <= now()
ORDER BY
    post.published_at DESC,
    post.id DESC;
$$;
