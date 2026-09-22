DROP FUNCTION IF EXISTS get_artwork_list;

CREATE FUNCTION get_artwork_list (
    p_artwork_type_ids int[],
    p_vocabulary_term_ids int[],
    p_matching_artwork_ids int[],
    -- an empty list of matches means the search found nothing, which is not the same
    -- as not searching at all
    p_is_searching boolean,
    p_series_id int,
    p_has_images boolean,
    p_is_for_sale boolean,
    p_sort smallint,
    p_offset int,
    p_page_size int
) RETURNS TABLE (
    id int,
    name text,
    slug text,
    artwork_type_name text,
    date_created date,
    date_created_precision smallint,
    image_count int,
    is_for_sale boolean,
    series_names text,
    primary_image_storage_key text,
    primary_image_width int,
    primary_image_height int,
    primary_image_blur_data_uri text,
    total_count int
) LANGUAGE plpgsql STABLE AS $$
DECLARE
    -- must match the ArtworkListSort enum in C#
    recently_added CONSTANT smallint := 1;
    title_ascending CONSTANT smallint := 2;
    title_descending CONSTANT smallint := 3;
    date_created_newest CONSTANT smallint := 4;
    date_created_oldest CONSTANT smallint := 5;
    series_order CONSTANT smallint := 6;
    chosen_vocabulary_count int;
BEGIN
    -- how many vocabularies the chosen terms come from. An artwork has to match a term from every
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
    -- names its table, or Postgres couldn't tell artwork.name from the name being returned
    RETURN QUERY
    SELECT
        artwork.id,
        artwork.name::text,
        artwork.slug::text,
        artwork_type.name::text,
        artwork.date_created,
        artwork.date_created_precision,
        summary.image_count,
        summary.is_for_sale,
        (
            -- string_agg takes its own ORDER BY, in place of WITHIN GROUP
            SELECT
                string_agg(
                    series.name,
                    ', '
                    ORDER BY
                        series.sort_order
                )
            FROM
                artwork_and_series_junction AS junction
                JOIN series ON series.id = junction.series_id
            WHERE
                junction.artwork_id = artwork.id
        ),
        primary_image.storage_key::text,
        primary_image.width,
        primary_image.height,
        primary_image.blur_data_uri::text,
        -- the whole filtered count, repeated on every row of this page
        (COUNT(*) OVER ())::int
    FROM
        artworks AS artwork
        JOIN artwork_types AS artwork_type ON artwork_type.id = artwork.artwork_type_id
        -- unique_index_artwork_images_primary allows one primary row per artwork, so this join can
        -- only ever add one row. The same rule get_series_with_covers and get_series_artworks read
        -- a cover by
        LEFT JOIN artwork_images AS primary_image ON primary_image.artwork_id = artwork.id
        AND primary_image.is_primary
        -- worked out once, for the columns above and the filters below. A LATERAL subquery is part
        -- of FROM, so unlike a column alias its results can be used in WHERE
        CROSS JOIN LATERAL (
            SELECT
                (
                    SELECT
                        COUNT(*)::int
                    FROM
                        artwork_images AS image
                    WHERE
                        image.artwork_id = artwork.id
                ) AS image_count,
                EXISTS (
                    SELECT
                    FROM
                        products AS product
                    WHERE
                        product.artwork_id = artwork.id
                        AND product.stock >= 1
                ) AS is_for_sale
        ) AS summary
        -- where the artist dragged this artwork inside the series being looked at. NULL under
        -- every other filter, which is why series_order only means anything with a series chosen
        LEFT JOIN artwork_and_series_junction AS series_place ON series_place.artwork_id = artwork.id
        AND series_place.series_id = p_series_id
    WHERE
        (
            cardinality(p_artwork_type_ids) = 0
            OR artwork.artwork_type_id = ANY (p_artwork_type_ids)
        )
        AND (
            p_series_id IS NULL
            OR series_place.artwork_id IS NOT NULL
        )
        AND (
            NOT p_is_searching
            OR artwork.id = ANY (p_matching_artwork_ids)
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
            -- artwork matches needs no join back to the terms
            OR (
                SELECT
                    COUNT(DISTINCT artwork_term.vocabulary_id)
                FROM
                    artwork_and_vocabulary_terms_junction AS artwork_term
                WHERE
                    artwork_term.artwork_id = artwork.id
                    AND artwork_term.term_id = ANY (p_vocabulary_term_ids)
            ) = chosen_vocabulary_count
        )
    ORDER BY
        -- an undated artwork sinks to the bottom of either date sort rather than leading
        -- the oldest-first one. Every row gets 0 under the other sorts, so it does nothing there
        CASE
            WHEN p_sort IN (date_created_newest, date_created_oldest)
            AND artwork.date_created IS NULL THEN 1
            ELSE 0
        END,
        -- only the chosen sort's CASE has a value; the rest are NULL for every row, which sorts
        -- everything equal and so changes nothing
        CASE
            WHEN p_sort = recently_added THEN artwork.created_at
        END DESC,
        CASE
            WHEN p_sort = title_ascending THEN artwork.name
        END,
        CASE
            WHEN p_sort = title_descending THEN artwork.name
        END DESC,
        CASE
            WHEN p_sort = date_created_newest THEN artwork.date_created
        END DESC,
        CASE
            WHEN p_sort = date_created_oldest THEN artwork.date_created
        END,
        CASE
            WHEN p_sort = series_order THEN series_place.sort_order
        END,
        -- the tie-break that stops a row moving between pages
        artwork.id
    LIMIT
        p_page_size
    OFFSET
        p_offset;
END;
$$;
