DROP FUNCTION IF EXISTS set_page_row_settings;

-- all of RowSettings at once: a saved theme or a ThemePreset's id or neither; the background's
-- RowBackgroundType, hex colour, and for an image its source, if chosen, and ImageFit
CREATE FUNCTION set_page_row_settings (
    p_row_id int,
    p_height int,
    p_content_width int,
    p_top_padding int,
    p_bottom_padding int,
    p_side_padding int,
    p_gap int,
    p_theme_id int,
    p_theme_preset int,
    p_background_type int,
    p_background_color text,
    p_background_uploaded_image_key text,
    p_background_work_id int,
    p_background_work_image_key text,
    p_background_fit int
) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- a theme deleted since the page loaded, named rather than left to the foreign key's error
    IF p_theme_id IS NOT NULL THEN
        PERFORM 1 FROM themes WHERE id = p_theme_id;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'The theme no longer exists.' USING ERRCODE = 'SH019';
        END IF;
    END IF;

    -- an image deleted since the form checked it, named rather than left to the foreign key's error
    IF p_background_uploaded_image_key IS NOT NULL THEN
        PERFORM 1 FROM uploaded_images WHERE storage_key = p_background_uploaded_image_key;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'The image no longer exists.' USING ERRCODE = 'SH029';
        END IF;
    END IF;

    IF p_background_work_id IS NOT NULL THEN
        PERFORM 1 FROM works WHERE id = p_background_work_id;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'The work no longer exists.' USING ERRCODE = 'SH003';
        END IF;
    END IF;

    UPDATE page_rows
    SET
        height = p_height,
        content_width = p_content_width,
        top_padding = p_top_padding,
        bottom_padding = p_bottom_padding,
        side_padding = p_side_padding,
        gap = p_gap,
        theme_id = p_theme_id,
        theme_preset = p_theme_preset,
        background_type = p_background_type,
        background_color = p_background_color,
        background_uploaded_image_key = p_background_uploaded_image_key,
        background_work_id = p_background_work_id,
        background_work_image_key = p_background_work_image_key,
        background_fit = p_background_fit
    WHERE
        id = p_row_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The row has changed since the page loaded.' USING ERRCODE = 'SH024';
    END IF;
END;
$$;
