DROP FUNCTION IF EXISTS update_work_images;

-- The images only, for the work table's images dialog, whose rows save their other fields on their
-- own. The lock makes an upload appending to this work wait, as update_work's does
CREATE FUNCTION update_work_images (p_id int, p_images work_image_input[]) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    PERFORM
    FROM
        works AS work
    WHERE
        work.id = p_id
    FOR NO KEY UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The work no longer exists.' USING ERRCODE = 'SH003';
    END IF;

    PERFORM set_work_images(p_id, p_images);
END;
$$;
