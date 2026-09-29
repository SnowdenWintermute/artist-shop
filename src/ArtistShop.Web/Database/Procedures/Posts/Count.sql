DROP FUNCTION IF EXISTS count_posts;

-- drafts included
CREATE FUNCTION count_posts () RETURNS int LANGUAGE sql STABLE AS $$
SELECT
    COUNT(*)::int
FROM
    posts AS post;
$$;
