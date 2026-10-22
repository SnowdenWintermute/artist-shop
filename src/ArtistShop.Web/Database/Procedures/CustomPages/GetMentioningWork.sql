DROP FUNCTION IF EXISTS get_custom_pages_mentioning_work;

-- the pages with a text component on them that embeds the work, an image component showing one of its
-- images, or a card grid whose custom list shows it. Every custom page is public
CREATE FUNCTION get_custom_pages_mentioning_work (p_work_id int) RETURNS TABLE (
    id int,
    name text,
    address text,
    is_home boolean,
    is_blog boolean,
    is_collections boolean
) LANGUAGE sql STABLE AS $$
SELECT DISTINCT
    custom_pages.id,
    custom_pages.name,
    custom_pages.address,
    home_page.page_id IS NOT NULL,
    blog_page.page_id IS NOT NULL,
    collections_page.page_id IS NOT NULL
FROM
    (
        SELECT
            junction.component_id
        FROM
            custom_component_and_works_junction AS junction
            JOIN custom_components AS component ON component.id = junction.component_id
        WHERE
            junction.work_id = p_work_id
            -- an image component only while a visitor sees the image: the one picked, or the work's main image,
            -- which any work with images has. CustomComponentType.Image
            AND (
                component.type <> 4
                OR EXISTS (
                    SELECT
                    FROM
                        work_images AS image
                    WHERE
                        image.work_id = p_work_id
                        AND (
                            component.settings #>> '{source,storageKey}' IS NULL
                            OR image.storage_key = component.settings #>> '{source,storageKey}'
                        )
                )
            )
        UNION
        -- only while the grid shows its custom list, and only a work a visitor sees there: one with the
        -- primary image its card shows
        SELECT
            chosen.component_id
        FROM
            card_grid_chosen_cards AS chosen
            JOIN custom_components AS component ON component.id = chosen.component_id
        WHERE
            chosen.work_id = p_work_id
            AND component.settings #>> '{source,type}' = 'ChosenCards'
            AND EXISTS (
                SELECT
                FROM
                    work_images AS image
                WHERE
                    image.work_id = p_work_id
                    AND image.is_primary
            )
    ) AS mentioning
    JOIN slot_placements ON slot_placements.component_id = mentioning.component_id
    JOIN row_slots ON row_slots.id = slot_placements.slot_id
    JOIN page_rows ON page_rows.id = row_slots.row_id
    JOIN custom_pages ON custom_pages.id = page_rows.page_id
    LEFT JOIN home_page ON home_page.page_id = custom_pages.id
    LEFT JOIN blog_page ON blog_page.page_id = custom_pages.id
    LEFT JOIN collections_page ON collections_page.page_id = custom_pages.id;
$$;
