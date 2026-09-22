DROP FUNCTION IF EXISTS get_series_with_covers;

CREATE FUNCTION get_series_with_covers (p_only_artworks_with_images boolean) RETURNS TABLE (
    id int,
    name text,
    slug text,
    artwork_count int,
    cover_storage_key text,
    cover_original_file_name text,
    cover_width int,
    cover_height int,
    cover_blur_data_uri text
) LANGUAGE sql STABLE AS $$
SELECT
    series.id,
    series.name,
    series.slug,
    (
        SELECT
            COUNT(*)::int
        FROM
            artwork_and_series_junction AS junction
        WHERE
            junction.series_id = series.id
            AND (
                NOT p_only_artworks_with_images
                OR EXISTS (
                    SELECT
                    FROM
                        artwork_images AS image
                    WHERE
                        image.artwork_id = junction.artwork_id
                )
            )
    ),
    cover.storage_key,
    cover.original_file_name,
    cover.width,
    cover.height,
    cover.blur_data_uri
FROM
    series
    -- LATERAL lets the subquery use the series from its own row, so it runs once per series the
    -- way OUTER APPLY does. LEFT JOIN ... ON true keeps a series with no match
    LEFT JOIN LATERAL (
        SELECT
            primary_image.storage_key,
            primary_image.original_file_name,
            primary_image.width,
            primary_image.height,
            primary_image.blur_data_uri
        FROM
            artwork_and_series_junction AS junction
            -- an inner join, so artworks with no images can't become the cover
            JOIN artwork_images AS primary_image ON primary_image.artwork_id = junction.artwork_id
            AND primary_image.is_primary
        WHERE
            junction.series_id = series.id
        ORDER BY
            -- true sorts after false, so DESC puts the starred cover first
            junction.is_cover DESC,
            junction.sort_order
        LIMIT
            1
    ) AS cover ON true
WHERE
    -- a series whose artworks have no photographs yet has nothing a visitor could look at
    NOT p_only_artworks_with_images
    OR cover.storage_key IS NOT NULL
ORDER BY
    series.sort_order;
$$;
