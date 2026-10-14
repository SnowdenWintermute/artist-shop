DROP FUNCTION IF EXISTS get_collections_and_loose_works_page;

-- What a visitor may see: the collections with something to look at, in the artist's order, then the works
-- with an image that are in no collection, in p_loose_work_sort's order (WorkListSort's values; the caller
-- chooses, so changing it needs no migration). card_type is 1 for a collection and 2 for a work. plpgsql,
-- so get_collections_with_covers needn't exist yet when this file runs
CREATE FUNCTION get_collections_and_loose_works_page (p_loose_work_sort smallint, p_offset int, p_page_size int) RETURNS TABLE (
    card_type smallint,
    id int,
    name text,
    slug text,
    work_count int,
    image_storage_key text,
    image_original_file_name text,
    image_width int,
    image_height int,
    image_blur_data_uri text,
    total_count int
) LANGUAGE plpgsql STABLE AS $$
DECLARE
    -- must match the WorkListSort enum in C#
    recently_added CONSTANT smallint := 1;
    title_ascending CONSTANT smallint := 2;
    title_descending CONSTANT smallint := 3;
    date_created_newest CONSTANT smallint := 4;
    date_created_oldest CONSTANT smallint := 5;
BEGIN
    RETURN QUERY
    SELECT
        card.card_type,
        card.id,
        card.name,
        card.slug,
        card.work_count,
        card.storage_key,
        card.original_file_name,
        card.width,
        card.height,
        card.blur_data_uri,
        (COUNT(*) OVER ())::int
    FROM
        (
            SELECT
                1::smallint AS card_type,
                collection.id,
                collection.name,
                collection.slug,
                collection.work_count,
                collection.cover_storage_key AS storage_key,
                collection.cover_original_file_name AS original_file_name,
                collection.cover_width AS width,
                collection.cover_height AS height,
                collection.cover_blur_data_uri AS blur_data_uri,
                -- the function returns them in the artist's order
                collection.place AS collection_place,
                NULL::timestamptz AS created_at,
                NULL::date AS date_created
            FROM
                get_collections_with_covers (true) WITH ORDINALITY AS collection (
                    id,
                    name,
                    slug,
                    work_count,
                    cover_storage_key,
                    cover_original_file_name,
                    cover_width,
                    cover_height,
                    cover_blur_data_uri,
                    place
                )
            UNION ALL
            SELECT
                2::smallint,
                work.id,
                work.name::text,
                work.slug::text,
                NULL::int,
                primary_image.storage_key::text,
                primary_image.original_file_name::text,
                primary_image.width,
                primary_image.height,
                primary_image.blur_data_uri::text,
                NULL::bigint,
                work.created_at,
                work.date_created
            FROM
                works AS work
                -- an inner join: a work with no image has nothing to show
                JOIN work_images AS primary_image ON primary_image.work_id = work.id
                AND primary_image.is_primary
            WHERE
                NOT EXISTS (
                    SELECT
                    FROM
                        work_and_collection_junction AS junction
                    WHERE
                        junction.work_id = work.id
                )
        ) AS card
    ORDER BY
        card.card_type,
        card.collection_place,
        -- as get_work_list sorts: an undated work sinks under either date sort, and only the chosen sort's
        -- CASE has a value
        CASE
            WHEN p_loose_work_sort IN (date_created_newest, date_created_oldest)
            AND card.date_created IS NULL THEN 1
            ELSE 0
        END,
        CASE
            WHEN p_loose_work_sort = recently_added THEN card.created_at
        END DESC,
        CASE
            WHEN p_loose_work_sort = title_ascending THEN card.name
        END,
        CASE
            WHEN p_loose_work_sort = title_descending THEN card.name
        END DESC,
        CASE
            WHEN p_loose_work_sort = date_created_newest THEN card.date_created
        END DESC,
        CASE
            WHEN p_loose_work_sort = date_created_oldest THEN card.date_created
        END,
        card.id
    LIMIT
        p_page_size
    OFFSET
        p_offset;
END;
$$;
