DROP FUNCTION IF EXISTS update_artwork;

-- returns the slug, because this decides it: a rename can land on a numbered one
CREATE FUNCTION update_artwork (
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
    p_images artwork_image_input[],
    p_vocabulary_term_ids int[],
    p_series_ids int[]
) RETURNS text LANGUAGE plpgsql AS $$
DECLARE
    current_artwork_type_id int;
    current_slug text;
    new_slug text;
BEGIN
    -- An artwork's type never changes, so it isn't a parameter: it's read from the row, which the
    -- vocabulary junction copies and which decides the allowed fields. FOR UPDATE holds the row
    -- until the transaction ends, so a delete in another tab waits rather than landing between
    -- this read and the UPDATE
    SELECT
        artwork.artwork_type_id,
        artwork.slug
    INTO
        current_artwork_type_id,
        current_slug
    FROM
        artworks AS artwork
    WHERE
        artwork.id = p_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The artwork no longer exists.' USING ERRCODE = 'SH003';
    END IF;

    PERFORM check_artwork_choices_are_current(
        current_artwork_type_id,
        p_date_created,
        p_height_cm,
        p_width_cm,
        p_depth_cm,
        p_duration_seconds,
        p_vocabulary_term_ids,
        p_series_ids
    );

    -- Keep the slug when it already belongs to this name's family, so saving an unchanged form
    -- doesn't move sunset-2 to sunset-4. Otherwise the current slug is outside the family, so this
    -- artwork's own row can't be what makes resolve_artwork_slug add a number
    new_slug := CASE
        WHEN current_slug = p_candidate_slug
        OR artwork_slug_number(current_slug, p_candidate_slug) IS NOT NULL THEN current_slug
        ELSE resolve_artwork_slug(p_candidate_slug)
    END;

    UPDATE artworks
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

    PERFORM set_artwork_images(p_id, p_images);

    PERFORM set_artwork_vocabulary_terms(p_id, current_artwork_type_id, p_vocabulary_term_ids);

    PERFORM set_artwork_series(p_id, p_series_ids);

    RETURN new_slug;
END;
$$;
