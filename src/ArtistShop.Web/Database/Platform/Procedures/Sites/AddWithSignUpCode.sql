DROP FUNCTION IF EXISTS add_site_with_sign_up_code;

-- Sign-up's one write to the platform database: uses up the code and adds the site, reached by
-- p_host and owned by p_owner_user_id, together. A function runs as one transaction, so if the host
-- is taken the code isn't used, and two sign-ups with one code can't both make a site
CREATE FUNCTION add_site_with_sign_up_code (p_code_hash bytea, p_host text, p_owner_user_id text) RETURNS int LANGUAGE plpgsql AS $$
BEGIN
    -- deleting the row locks it, so a second sign-up with the same code waits here, then finds it gone
    DELETE FROM sign_up_codes
    WHERE
        code_hash = p_code_hash
        AND expires_at > clock_timestamp();

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The sign-up code was never made, has expired, or has been used.' USING ERRCODE = 'SH016';
    END IF;

    RETURN add_site (ARRAY[p_host], p_owner_user_id);
END;
$$;
