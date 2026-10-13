DROP FUNCTION IF EXISTS set_post_works;

-- Rebuilds the post's junction rows from its body, so they can't disagree with what the post shows.
-- plpgsql, so rich_text_work_ids is looked up when it runs and the files can run in any order
CREATE FUNCTION set_post_works (p_post_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
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
        CROSS JOIN LATERAL rich_text_work_ids(post.body) AS embedded (work_id)
        -- a work deleted after the editor loaded leaves an embed that renders nothing, not an error
        JOIN works AS work ON work.id = embedded.work_id
    WHERE
        post.id = p_post_id;
END;
$$;
