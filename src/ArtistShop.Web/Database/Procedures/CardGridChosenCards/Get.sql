DROP FUNCTION IF EXISTS get_card_grid_chosen_cards;

-- A card grid's custom list in the artist's order, each row a collection or a work. The image is the
-- collection's cover or the work's primary image, and is NULL for one a visitor wouldn't see: a collection
-- with no work with an image, or a work with no image. A collection's count is of its works with images,
-- as a visitor's card shows. plpgsql, so get_collections_with_covers is looked up when it runs and the files
-- can run in any order; every column names its table, as the returned columns are variables here
CREATE FUNCTION get_card_grid_chosen_cards (p_component_id int) RETURNS TABLE (
    id int,
    collection_id int,
    collection_name text,
    collection_slug text,
    collection_work_count int,
    work_id int,
    work_name text,
    work_slug text,
    image_storage_key text,
    image_original_file_name text,
    image_width int,
    image_height int,
    image_blur_data_uri text
) LANGUAGE plpgsql STABLE AS $$
BEGIN
    RETURN QUERY
    SELECT
        chosen.id,
        collections.id,
        collections.name::text,
        collections.slug::text,
        visible_collection.work_count,
        work.id,
        work.name::text,
        work.slug::text,
        COALESCE(visible_collection.cover_storage_key, primary_image.storage_key::text),
        COALESCE(visible_collection.cover_original_file_name, primary_image.original_file_name::text),
        COALESCE(visible_collection.cover_width, primary_image.width),
        COALESCE(visible_collection.cover_height, primary_image.height),
        COALESCE(visible_collection.cover_blur_data_uri, primary_image.blur_data_uri::text)
    FROM
        card_grid_chosen_cards AS chosen
        LEFT JOIN collections ON collections.id = chosen.collection_id
        -- only the collections a visitor sees come back, so one missing here is one with nothing to show
        LEFT JOIN get_collections_with_covers (true, NULL) AS visible_collection ON visible_collection.id = chosen.collection_id
        LEFT JOIN works AS work ON work.id = chosen.work_id
        LEFT JOIN work_images AS primary_image ON primary_image.work_id = chosen.work_id
        AND primary_image.is_primary
    WHERE
        chosen.component_id = p_component_id
    ORDER BY
        chosen.sort_order;
END;
$$;
