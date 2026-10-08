DROP FUNCTION IF EXISTS set_collection_cover;

CREATE FUNCTION set_collection_cover (p_collection_id int, p_work_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (
        SELECT
        FROM
            work_images
        WHERE
            work_id = p_work_id
            AND is_primary
    ) THEN
        RAISE EXCEPTION 'The work has no image to use as the cover.' USING ERRCODE = 'SH007';
    END IF;

    -- two statements, old star off first: a unique index checks each row as it changes and, unlike
    -- a constraint, can't be DEFERRABLE, so one UPDATE could briefly hold two covers and fail
    UPDATE work_and_collection_junction
    SET
        is_cover = false
    WHERE
        collection_id = p_collection_id
        AND is_cover;

    UPDATE work_and_collection_junction
    SET
        is_cover = true
    WHERE
        collection_id = p_collection_id
        AND work_id = p_work_id;

    -- Checked here rather than before the first UPDATE, so a work taken out of the collection in
    -- between can't leave the collection with no star and no error. The RAISE rolls back the UPDATE
    -- above as well: an error undoes the whole call
    IF NOT FOUND THEN
        RAISE EXCEPTION 'The work is no longer in the collection.' USING ERRCODE = 'SH006';
    END IF;
END;
$$;
