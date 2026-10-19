DROP FUNCTION IF EXISTS clear_row_backgrounds_showing;

-- Takes these works or library images out of the row backgrounds showing them, as they're deleted,
-- keeping a colour behind the image as the row's colour background (Mike, 2026-10-18)
CREATE FUNCTION clear_row_backgrounds_showing (p_work_ids int[], p_storage_keys text[]) RETURNS void LANGUAGE sql AS $$
UPDATE page_rows
SET
    -- RowBackgroundType.None or Color
    background_type = CASE
        WHEN background_color IS NULL THEN 1
        ELSE 2
    END,
    background_uploaded_image_key = NULL,
    background_work_id = NULL,
    background_work_image_key = NULL,
    background_fit = NULL
WHERE
    background_work_id = ANY (p_work_ids)
    OR background_uploaded_image_key = ANY (p_storage_keys);
$$;
