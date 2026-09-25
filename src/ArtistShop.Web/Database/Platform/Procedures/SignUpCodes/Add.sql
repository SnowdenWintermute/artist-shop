DROP FUNCTION IF EXISTS add_sign_up_code;

CREATE FUNCTION add_sign_up_code (p_code_hash bytea, p_note text, p_expires_at timestamptz) RETURNS int LANGUAGE sql AS $$
INSERT INTO
    sign_up_codes (code_hash, note, expires_at)
VALUES
    (p_code_hash, p_note, p_expires_at)
RETURNING
    id;
$$;
