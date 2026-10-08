DROP FUNCTION IF EXISTS update_work_details;

-- Everything update_work sets but the images, for the add-from-images table: its uploads append
-- images while rows are saved, and posting a whole image list would drop the ones still arriving.
-- Returns the slug, because this decides it: a rename can land on a numbered one
CREATE FUNCTION update_work_details (
    p_id int,
    p_name text,
    p_candidate_slug text,
    p_description text,
    p_date_created date,
    p_date_created_precision smallint,
    p_height_cm numeric,
    p_width_cm numeric,
    p_depth_cm numeric,
    p_duration_seconds int,
    p_vocabulary_term_ids int[],
    p_collection_ids int[]
) RETURNS text LANGUAGE plpgsql AS $$
DECLARE
    current_work_type_id int;
    current_slug text;
    new_slug text;
BEGIN
    -- A work's type never changes, so it isn't a parameter: it's read from the row, which the
    -- vocabulary junction copies and which decides the allowed fields. Read without a lock, since
    -- the value can't change underneath us
    SELECT
        work.work_type_id
    INTO
        current_work_type_id
    FROM
        works AS work
    WHERE
        work.id = p_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The work no longer exists.' USING ERRCODE = 'SH003';
    END IF;

    -- locks the type row before the work row below, the order update_work_type takes them in.
    -- The other way round, each could hold the row the other is waiting for
    PERFORM check_work_choices_are_current(
        current_work_type_id,
        p_date_created,
        p_height_cm,
        p_width_cm,
        p_depth_cm,
        p_duration_seconds,
        p_vocabulary_term_ids,
        p_collection_ids
    );

    -- NO KEY UPDATE holds the row until the transaction ends, so a delete in another tab waits
    -- rather than landing between here and the UPDATE. It's the lock the UPDATE takes anyway, and
    -- unlike FOR UPDATE it lets other rows' foreign key checks against this work through
    SELECT
        work.slug
    INTO
        current_slug
    FROM
        works AS work
    WHERE
        work.id = p_id
    FOR NO KEY UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The work no longer exists.' USING ERRCODE = 'SH003';
    END IF;

    -- Keep the slug when it already belongs to this name's family, so saving an unchanged form
    -- doesn't move sunset-2 to sunset-4. Otherwise the current slug is outside the family, so this
    -- work's own row can't be what makes resolve_work_slug add a number
    new_slug := CASE
        WHEN current_slug = p_candidate_slug
        OR work_slug_number(current_slug, p_candidate_slug) IS NOT NULL THEN current_slug
        ELSE resolve_work_slug(p_candidate_slug)
    END;

    UPDATE works
    SET
        name = p_name,
        slug = new_slug,
        description = p_description,
        date_created = p_date_created,
        date_created_precision = p_date_created_precision,
        height_cm = p_height_cm,
        width_cm = p_width_cm,
        depth_cm = p_depth_cm,
        duration_seconds = p_duration_seconds
    WHERE
        id = p_id;

    PERFORM set_work_vocabulary_terms(p_id, current_work_type_id, p_vocabulary_term_ids);

    PERFORM set_work_collections(p_id, p_collection_ids);

    RETURN new_slug;
END;
$$;
