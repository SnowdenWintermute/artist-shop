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
    new_slug text;
BEGIN
    -- holds the artwork's row lock until the transaction ends, so the images below are set under it
    new_slug := update_artwork_details(
        p_id,
        p_name,
        p_candidate_slug,
        p_description,
        p_date_created,
        p_date_created_precision,
        p_height_cm,
        p_width_cm,
        p_depth_cm,
        p_duration_seconds,
        p_vocabulary_term_ids,
        p_series_ids
    );

    PERFORM set_artwork_images(p_id, p_images);

    RETURN new_slug;
END;
$$;
