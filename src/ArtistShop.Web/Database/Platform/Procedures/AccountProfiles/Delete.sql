DROP FUNCTION IF EXISTS delete_account_profile;

CREATE FUNCTION delete_account_profile (p_user_id text) RETURNS void LANGUAGE sql AS $$
DELETE FROM account_profiles AS profile
WHERE
    profile.user_id = p_user_id;
$$;
