DROP FUNCTION IF EXISTS post_body_work_ids;

-- The works a text from the editor embeds: a post's body, or an article's under "body" in its settings.
-- A jsonpath: every op, then its insert, then a work embed's id (a key with a hyphen is quoted). The
-- default lax mode skips whatever lacks a step, like a text insert, which has no "artshop-work" key.
-- The ? (...) filter keeps only whole numbers an int can hold, as the page's parser does: the cast
-- would round 1.5 to 2, and fail the save on a string or on a number out of range
CREATE FUNCTION post_body_work_ids (p_body jsonb) RETURNS SETOF int LANGUAGE sql IMMUTABLE AS $$
SELECT
    embedded.work_id::int
FROM
    jsonb_path_query(
        p_body,
        '$.ops[*].insert."artshop-work".workId ? (@.type() == "number" && @ == @.floor() && @ >= -2147483648 && @ <= 2147483647)'
    ) AS embedded (work_id);
$$;
