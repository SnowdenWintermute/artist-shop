DROP FUNCTION IF EXISTS get_account_dismissed_hints;

CREATE FUNCTION get_account_dismissed_hints (p_user_id text) RETURNS TABLE (hint text) LANGUAGE sql STABLE AS $$
SELECT
    dismissed.hint
FROM
    account_dismissed_hints AS dismissed
WHERE
    dismissed.user_id = p_user_id;
$$;
