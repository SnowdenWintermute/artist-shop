DROP FUNCTION IF EXISTS sign_up_code_is_usable;

-- Sign-up's check before it makes the site's schema, so a mistyped code doesn't make one only to
-- drop it. add_site_with_sign_up_code checks again as it uses the code
CREATE FUNCTION sign_up_code_is_usable (p_code_hash bytea) RETURNS boolean LANGUAGE sql STABLE AS $$
SELECT
    EXISTS (
        SELECT
        FROM
            sign_up_codes
        WHERE
            code_hash = p_code_hash
            AND expires_at > clock_timestamp()
    );
$$;
