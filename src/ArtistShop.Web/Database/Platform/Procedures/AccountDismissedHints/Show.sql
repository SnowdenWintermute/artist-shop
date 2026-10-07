DROP FUNCTION IF EXISTS show_account_hints;

CREATE FUNCTION show_account_hints (p_user_id text, p_hints text[]) RETURNS void LANGUAGE sql AS $$
DELETE FROM account_dismissed_hints AS dismissed
WHERE
    dismissed.user_id = p_user_id
    AND dismissed.hint = ANY (p_hints);
$$;
