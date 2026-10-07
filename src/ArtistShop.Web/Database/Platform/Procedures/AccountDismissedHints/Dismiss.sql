DROP FUNCTION IF EXISTS dismiss_account_hints;

-- Read from account_profiles rather than inserted outright, so an account deleted since its page
-- loaded saves nothing, where the foreign key would have failed the insert
CREATE FUNCTION dismiss_account_hints (p_user_id text, p_hints text[]) RETURNS void LANGUAGE sql AS $$
INSERT INTO
    account_dismissed_hints (user_id, hint)
SELECT
    profile.user_id,
    hint
FROM
    account_profiles AS profile
    CROSS JOIN unnest(p_hints) AS hint
WHERE
    profile.user_id = p_user_id
ON CONFLICT (user_id, hint) DO NOTHING;
$$;
