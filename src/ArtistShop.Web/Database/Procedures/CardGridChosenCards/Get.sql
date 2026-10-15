DROP FUNCTION IF EXISTS get_card_grid_chosen_cards;

-- A card grid's custom list in the artist's order, each row a collection or a work. The image is the
-- collection's cover or the work's primary image, and is NULL for one a visitor wouldn't see: a collection
-- with no work with an image, or a work with no image. A collection's count is of its works with images,
-- as a visitor's card shows. p_only_visible leaves those out; p_offset and p_count take a slice (NULL count
-- for the rest), and total_count is of every row before the slice. plpgsql, so get_collections_with_covers
-- is looked up when it runs and the files can run in any order; every column names its table, as the
-- returned columns are variables here
CREATE FUNCTION get_card_grid_chosen_cards (p_component_id int, p_only_visible boolean, p_offset int, p_count int) RETURNS TABLE (
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
    image_blur_data_uri text,
    total_count int
) LANGUAGE plpgsql STABLE AS $$
BEGIN
    RETURN QUERY
    WITH
        card AS (
            SELECT
                chosen.id AS card_id,
                chosen.sort_order,
                collections.id AS card_collection_id,
                collections.name::text AS card_collection_name,
                collections.slug::text AS card_collection_slug,
                visible_collection.work_count AS card_collection_work_count,
                work.id AS card_work_id,
                work.name::text AS card_work_name,
                work.slug::text AS card_work_slug,
                COALESCE(visible_collection.cover_storage_key, primary_image.storage_key::text) AS card_storage_key,
                COALESCE(visible_collection.cover_original_file_name, primary_image.original_file_name::text) AS card_original_file_name,
                COALESCE(visible_collection.cover_width, primary_image.width) AS card_width,
                COALESCE(visible_collection.cover_height, primary_image.height) AS card_height,
                COALESCE(visible_collection.cover_blur_data_uri, primary_image.blur_data_uri::text) AS card_blur_data_uri
            FROM
                card_grid_chosen_cards AS chosen
                LEFT JOIN collections ON collections.id = chosen.collection_id
                -- only the collections a visitor sees come back, so one missing here is one with nothing to show.
                -- Only the list's collections, so the covers and counts of the rest aren't worked out
                LEFT JOIN get_collections_with_covers (
                    true,
                    NULL,
                    ARRAY(
                        SELECT
                            listed.collection_id
                        FROM
                            card_grid_chosen_cards AS listed
                        WHERE
                            listed.component_id = p_component_id
                            AND listed.collection_id IS NOT NULL
                    )
                ) AS visible_collection ON visible_collection.id = chosen.collection_id
                LEFT JOIN works AS work ON work.id = chosen.work_id
                LEFT JOIN work_images AS primary_image ON primary_image.work_id = chosen.work_id
                AND primary_image.is_primary
            WHERE
                chosen.component_id = p_component_id
        )
    SELECT
        card.card_id,
        card.card_collection_id,
        card.card_collection_name,
        card.card_collection_slug,
        card.card_collection_work_count,
        card.card_work_id,
        card.card_work_name,
        card.card_work_slug,
        card.card_storage_key,
        card.card_original_file_name,
        card.card_width,
        card.card_height,
        card.card_blur_data_uri,
        -- counted before OFFSET and LIMIT
        COUNT(*) OVER ()::int
    FROM
        card
    WHERE
        NOT p_only_visible
        OR card.card_storage_key IS NOT NULL
    ORDER BY
        card.sort_order
    OFFSET
        p_offset
    LIMIT
        p_count;
END;
$$;
