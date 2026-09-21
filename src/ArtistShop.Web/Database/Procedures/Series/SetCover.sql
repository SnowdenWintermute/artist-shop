DROP FUNCTION IF EXISTS set_series_cover;

CREATE FUNCTION set_series_cover (p_series_id int, p_artwork_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (
        SELECT
        FROM
            artwork_and_series_junction
        WHERE
            series_id = p_series_id
            AND artwork_id = p_artwork_id
    ) THEN
        RAISE EXCEPTION 'The artwork is no longer in the series.' USING ERRCODE = 'SH006';
    END IF;

    IF NOT EXISTS (
        SELECT
        FROM
            artwork_images
        WHERE
            artwork_id = p_artwork_id
            AND is_primary
    ) THEN
        RAISE EXCEPTION 'The artwork has no image to use as the cover.' USING ERRCODE = 'SH007';
    END IF;

    -- two statements, old star off first: a unique index checks each row as it changes and, unlike
    -- a constraint, can't be DEFERRABLE, so one UPDATE could briefly hold two covers and fail
    UPDATE artwork_and_series_junction
    SET
        is_cover = false
    WHERE
        series_id = p_series_id
        AND is_cover;

    UPDATE artwork_and_series_junction
    SET
        is_cover = true
    WHERE
        series_id = p_series_id
        AND artwork_id = p_artwork_id;
END;
$$;
