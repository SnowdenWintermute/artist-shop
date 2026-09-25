DROP FUNCTION IF EXISTS get_sign_up_codes;

-- every unused code, expired or not, without its hash
CREATE FUNCTION get_sign_up_codes () RETURNS TABLE (id int, note text, created_at timestamptz, expires_at timestamptz) LANGUAGE sql STABLE AS $$
SELECT
    sign_up_code.id,
    sign_up_code.note,
    sign_up_code.created_at,
    sign_up_code.expires_at
FROM
    sign_up_codes AS sign_up_code;
$$;
