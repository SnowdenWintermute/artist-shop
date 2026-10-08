DROP FUNCTION IF EXISTS rename_collection;

CREATE FUNCTION rename_collection (p_id int, p_name text, p_slug text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE collections
    SET
        name = p_name,
        slug = p_slug
    WHERE
        id = p_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The collection no longer exists.' USING ERRCODE = 'SH004';
    END IF;
END;
$$;
