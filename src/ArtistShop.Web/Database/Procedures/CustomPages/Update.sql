DROP FUNCTION IF EXISTS update_custom_page;

CREATE FUNCTION update_custom_page (p_id int, p_name text, p_address text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE custom_pages
    SET
        name = p_name,
        address = p_address
    WHERE
        id = p_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The page no longer exists.' USING ERRCODE = 'SH020';
    END IF;
END;
$$;
