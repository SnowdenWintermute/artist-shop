DROP FUNCTION IF EXISTS rename_series;

CREATE FUNCTION rename_series (p_id int, p_name text, p_slug text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE series
    SET
        name = p_name,
        slug = p_slug
    WHERE
        id = p_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The series no longer exists.' USING ERRCODE = 'SH004';
    END IF;
END;
$$;
