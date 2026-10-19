DROP FUNCTION IF EXISTS get_pages_with_row_backgrounds_showing;

-- the pages with a row whose background shows any of these works or library images, which deleting
-- them takes out of
CREATE FUNCTION get_pages_with_row_backgrounds_showing (p_work_ids int[], p_storage_keys text[]) RETURNS TABLE (name text) LANGUAGE sql STABLE AS $$
SELECT DISTINCT
    custom_pages.name::text
FROM
    page_rows
    JOIN custom_pages ON custom_pages.id = page_rows.page_id
WHERE
    page_rows.background_work_id = ANY (p_work_ids)
    OR page_rows.background_uploaded_image_key = ANY (p_storage_keys);
$$;
