DROP FUNCTION IF EXISTS delete_expired_sign_up_codes;

-- the codes that expired before p_before, which the operator no longer needs to see
CREATE FUNCTION delete_expired_sign_up_codes (p_before timestamptz) RETURNS void LANGUAGE sql AS $$
DELETE FROM sign_up_codes
WHERE
    expires_at < p_before;
$$;
