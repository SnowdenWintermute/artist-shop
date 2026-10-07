DROP FUNCTION IF EXISTS add_account_profile;

-- on every sign-in, so nothing when the account has one already
CREATE FUNCTION add_account_profile (p_user_id text) RETURNS void LANGUAGE sql AS $$
INSERT INTO
    account_profiles (user_id)
VALUES
    (p_user_id)
ON CONFLICT (user_id) DO NOTHING;
$$;
