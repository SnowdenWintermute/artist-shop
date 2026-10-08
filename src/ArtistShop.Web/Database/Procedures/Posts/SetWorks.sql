DROP FUNCTION IF EXISTS set_post_works;

-- Rebuilds the post's junction rows from its body, so they can't disagree with what the post shows
CREATE FUNCTION set_post_works (p_post_id int) RETURNS void LANGUAGE sql AS $$
DELETE FROM post_and_works_junction
WHERE
    post_id = p_post_id;

INSERT INTO
    post_and_works_junction (post_id, work_id)
SELECT DISTINCT
    p_post_id,
    work.id
FROM
    posts AS post
    -- A jsonpath: every op, then its insert, then a work embed's id (a key with a hyphen is
    -- quoted). The default lax mode skips whatever lacks a step, like a text insert, which has no
    -- "artshop-work" key. The ? (...) filter keeps
    -- only whole numbers an int can hold, as the page's parser does: the cast below would round
    -- 1.5 to 2, and fail the save on a string or on a number out of range
    CROSS JOIN LATERAL jsonb_path_query(
        post.body,
        '$.ops[*].insert."artshop-work".workId ? (@.type() == "number" && @ == @.floor() && @ >= -2147483648 && @ <= 2147483647)'
    ) AS embedded (work_id)
    -- a work deleted after the editor loaded leaves an embed that renders nothing, not an error
    JOIN works AS work ON work.id = embedded.work_id::int
WHERE
    post.id = p_post_id;
$$;
