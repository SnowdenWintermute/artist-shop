DROP FUNCTION IF EXISTS get_slides;

-- The owner's slides in the artist's order, each with the image its source shows now. A work's slide shows the
-- picked image, or with none picked its main one: the starred image, or the first when none is starred.
-- image_place is the shown image's place among the work's, for its page's address. The image columns are null
-- for a work whose picked image was taken off it, or that has none
CREATE FUNCTION get_slides (p_component_id int, p_row_id int) RETURNS TABLE (
    id int,
    uploaded_image_key text,
    work_id int,
    work_image_key text,
    image_storage_key text,
    image_original_file_name text,
    image_width int,
    image_height int,
    image_blur_data_uri text,
    library_alt_text text,
    work_name text,
    work_slug text,
    image_place int,
    is_main_image boolean
) LANGUAGE sql STABLE AS $$
SELECT
    slides.id,
    slides.uploaded_image_key::text,
    slides.work_id,
    slides.work_image_key::text,
    COALESCE(library.storage_key, shown.storage_key)::text,
    COALESCE(library.original_file_name, shown.original_file_name)::text,
    COALESCE(library.width, shown.width),
    COALESCE(library.height, shown.height),
    COALESCE(library.blur_data_uri, shown.blur_data_uri)::text,
    library.alt_text::text,
    works.name::text,
    works.slug::text,
    shown.place,
    shown.is_main
FROM
    slides
    LEFT JOIN uploaded_images AS library ON library.storage_key = slides.uploaded_image_key
    LEFT JOIN works ON works.id = slides.work_id
    LEFT JOIN LATERAL (
        SELECT
            numbered.*
        FROM
            (
                SELECT
                    image.storage_key,
                    image.original_file_name,
                    image.width,
                    image.height,
                    image.blur_data_uri,
                    (row_number() OVER (ORDER BY image.sort_order) - 1)::int AS place,
                    -- the starred image, or the first when none is starred
                    row_number() OVER (ORDER BY image.is_primary DESC, image.sort_order) = 1 AS is_main
                FROM
                    work_images AS image
                WHERE
                    image.work_id = slides.work_id
            ) AS numbered
        WHERE
            numbered.storage_key = slides.work_image_key
            OR (
                slides.work_image_key IS NULL
                AND numbered.is_main
            )
    ) AS shown ON true
WHERE
    slides.component_id = p_component_id
    OR slides.row_id = p_row_id
ORDER BY
    slides.sort_order;
$$;
