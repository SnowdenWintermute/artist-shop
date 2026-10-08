DROP FUNCTION IF EXISTS get_published_posts_mentioning_work;

CREATE FUNCTION get_published_posts_mentioning_work (p_work_id int) RETURNS TABLE (
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
    post_and_works_junction AS junction
    JOIN posts AS post ON post.id = junction.post_id
WHERE
    junction.work_id = p_work_id
    AND post.published_at <= now()
ORDER BY
    post.published_at DESC,
    post.id DESC;
$$;
