DROP FUNCTION IF EXISTS get_slide_owners_showing;

-- The carousels and the pages with a row slideshow holding a slide of any of these works or library images,
-- which deleting them takes the slides out of: a carousel with the pages it's on, or a row as its page's name
-- with the component's columns null. A carousel or page appears once however many of its slides go. Only
-- owners showing their chosen slides: slides kept while a carousel or row shows something else go unmentioned
CREATE FUNCTION get_slide_owners_showing (p_work_ids int[], p_storage_keys text[]) RETURNS TABLE (
    component_id int,
    component_name text,
    page_names text[]
) LANGUAGE sql STABLE AS $$
SELECT
    component.id,
    component.name::text,
    COALESCE(
        (
            SELECT
                array_agg(DISTINCT page.name::text)
            FROM
                slot_placements AS placement
                JOIN row_slots AS slot ON slot.id = placement.slot_id
                JOIN page_rows AS page_row ON page_row.id = slot.row_id
                JOIN custom_pages AS page ON page.id = page_row.page_id
            WHERE
                placement.component_id = component.id
        ),
        '{}'
    )
FROM
    custom_components AS component
WHERE
    component.settings #>> '{source,type}' = 'ChosenSlides'
    AND component.id IN (
        SELECT
            slides.component_id
        FROM
            slides
        WHERE
            slides.work_id = ANY (p_work_ids)
            OR slides.uploaded_image_key = ANY (p_storage_keys)
    )
UNION ALL
SELECT DISTINCT
    NULL::int,
    NULL::text,
    ARRAY[page.name::text]
FROM
    slides
    JOIN page_rows ON page_rows.id = slides.row_id
    JOIN custom_pages AS page ON page.id = page_rows.page_id
WHERE
    (
        slides.work_id = ANY (p_work_ids)
        OR slides.uploaded_image_key = ANY (p_storage_keys)
    )
    -- RowBackgroundType.Slideshow, SlideSourceType.ChosenSlides
    AND page_rows.background_type = 4
    AND page_rows.background_slide_source = 2;
$$;
