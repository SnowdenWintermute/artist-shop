DROP FUNCTION IF EXISTS get_work_list;

CREATE FUNCTION get_work_list (
    p_work_type_ids int[],
    p_vocabulary_term_ids int[],
    p_matching_work_ids int[],
    -- an empty list of matches means the search found nothing, which is not the same
    -- as not searching at all
    p_is_searching boolean,
    p_collection_id int,
    -- only the works in no collection at all, which p_collection_id can't say as it's NULL for any collection
    p_is_in_no_collection boolean,
    -- the collection a work is being added to, so the list leaves out what's in it already
    p_excluded_collection_id int,
    p_has_images boolean,
    p_is_for_sale boolean,
    p_sort smallint,
    p_offset int,
    p_page_size int
) RETURNS TABLE (
    id int,
    name text,
    slug text,
    work_type_name text,
    date_created date,
    date_created_precision smallint,
    image_count int,
    is_for_sale boolean,
    collection_names text,
    primary_image_storage_key text,
    primary_image_width int,
    primary_image_height int,
    primary_image_blur_data_uri text,
    total_count int
) LANGUAGE plpgsql STABLE AS $$
DECLARE
    -- must match the WorkListSort enum in C#
    recently_added CONSTANT smallint := 1;
    title_ascending CONSTANT smallint := 2;
    title_descending CONSTANT smallint := 3;
    date_created_newest CONSTANT smallint := 4;
    date_created_oldest CONSTANT smallint := 5;
    collection_order CONSTANT smallint := 6;
    chosen_vocabulary_count int;
BEGIN
    -- how many vocabularies the chosen terms come from. A work has to match a term from every
    -- one of them, so ticking Oil and Pastel widens the search while ticking Paper as well narrows it
    SELECT
        COUNT(DISTINCT term.vocabulary_id)
    INTO
        chosen_vocabulary_count
    FROM
        vocabulary_terms AS term
    WHERE
        term.id = ANY (p_vocabulary_term_ids);

    -- the RETURNS TABLE columns are variables inside a plpgsql function, so every column below
    -- names its table, or Postgres couldn't tell work.name from the name being returned
    RETURN QUERY
    SELECT
        work.id,
        work.name::text,
        work.slug::text,
        work_type.name::text,
        work.date_created,
        work.date_created_precision,
        summary.image_count,
        summary.is_for_sale,
        (
            -- string_agg takes its own ORDER BY, in place of WITHIN GROUP
            SELECT
                string_agg(
                    collections.name,
                    ', '
                    ORDER BY
                        collections.sort_order
                )
            FROM
                work_and_collection_junction AS junction
                JOIN collections ON collections.id = junction.collection_id
            WHERE
                junction.work_id = work.id
        ),
        primary_image.storage_key::text,
        primary_image.width,
        primary_image.height,
        primary_image.blur_data_uri::text,
        -- the whole filtered count, repeated on every row of this page
        (COUNT(*) OVER ())::int
    FROM
        works AS work
        JOIN work_types AS work_type ON work_type.id = work.work_type_id
        -- unique_index_work_images_primary allows one primary row per work, so this join can
        -- only ever add one row. The same rule get_collections_with_covers and get_collection_works read
        -- a cover by
        LEFT JOIN work_images AS primary_image ON primary_image.work_id = work.id
        AND primary_image.is_primary
        -- worked out once, for the columns above and the filters below. A LATERAL subquery is part
        -- of FROM, so unlike a column alias its results can be used in WHERE
        CROSS JOIN LATERAL (
            SELECT
                (
                    SELECT
                        COUNT(*)::int
                    FROM
                        work_images AS image
                    WHERE
                        image.work_id = work.id
                ) AS image_count,
                EXISTS (
                    SELECT
                    FROM
                        products AS product
                    WHERE
                        product.work_id = work.id
                        AND product.stock >= 1
                ) AS is_for_sale
        ) AS summary
        -- where the artist dragged this work inside the collection being looked at. NULL under
        -- every other filter, which is why collection_order only means anything with a collection chosen
        LEFT JOIN work_and_collection_junction AS collection_place ON collection_place.work_id = work.id
        AND collection_place.collection_id = p_collection_id
    WHERE
        (
            cardinality(p_work_type_ids) = 0
            OR work.work_type_id = ANY (p_work_type_ids)
        )
        AND (
            p_collection_id IS NULL
            OR collection_place.work_id IS NOT NULL
        )
        AND (
            NOT p_is_in_no_collection
            OR NOT EXISTS (
                SELECT
                FROM
                    work_and_collection_junction AS any_collection
                WHERE
                    any_collection.work_id = work.id
            )
        )
        AND (
            p_excluded_collection_id IS NULL
            OR NOT EXISTS (
                SELECT
                FROM
                    work_and_collection_junction AS excluded
                WHERE
                    excluded.work_id = work.id
                    AND excluded.collection_id = p_excluded_collection_id
            )
        )
        AND (
            NOT p_is_searching
            OR work.id = ANY (p_matching_work_ids)
        )
        AND (
            p_has_images IS NULL
            OR p_has_images = (summary.image_count > 0)
        )
        AND (
            p_is_for_sale IS NULL
            OR p_is_for_sale = summary.is_for_sale
        )
        AND (
            chosen_vocabulary_count = 0
            -- the junction carries vocabulary_id of its own, so counting the vocabularies an
            -- work matches needs no join back to the terms
            OR (
                SELECT
                    COUNT(DISTINCT work_term.vocabulary_id)
                FROM
                    work_and_vocabulary_terms_junction AS work_term
                WHERE
                    work_term.work_id = work.id
                    AND work_term.term_id = ANY (p_vocabulary_term_ids)
            ) = chosen_vocabulary_count
        )
    ORDER BY
        -- an undated work sinks to the bottom of either date sort rather than leading
        -- the oldest-first one. Every row gets 0 under the other sorts, so it does nothing there
        CASE
            WHEN p_sort IN (date_created_newest, date_created_oldest)
            AND work.date_created IS NULL THEN 1
            ELSE 0
        END,
        -- only the chosen sort's CASE has a value; the rest are NULL for every row, which sorts
        -- everything equal and so changes nothing
        CASE
            WHEN p_sort = recently_added THEN work.created_at
        END DESC,
        CASE
            WHEN p_sort = title_ascending THEN work.name
        END,
        CASE
            WHEN p_sort = title_descending THEN work.name
        END DESC,
        CASE
            WHEN p_sort = date_created_newest THEN work.date_created
        END DESC,
        CASE
            WHEN p_sort = date_created_oldest THEN work.date_created
        END,
        CASE
            WHEN p_sort = collection_order THEN collection_place.sort_order
        END,
        -- the tie-break that stops a row moving between pages
        work.id
    LIMIT
        p_page_size
    OFFSET
        p_offset;
END;
$$;
