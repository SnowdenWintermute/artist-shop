DROP FUNCTION IF EXISTS delete_sign_up_code;

CREATE FUNCTION delete_sign_up_code (p_id int) RETURNS void LANGUAGE sql AS $$
DELETE FROM sign_up_codes
WHERE
    id = p_id;
$$;
