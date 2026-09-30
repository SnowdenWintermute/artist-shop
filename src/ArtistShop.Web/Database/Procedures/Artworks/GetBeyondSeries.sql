DROP FUNCTION IF EXISTS get_artworks_beyond_series;

CREATE FUNCTION get_artworks_beyond_series (p_series_id int, p_only_artworks_with_images boolean) RETURNS TABLE (
    previous_series_slug text,
    previous_artwork_slug text,
    previous_image_count int,
    next_series_slug text,
    next_artwork_slug text,
    next_image_count int
) LANGUAGE sql STABLE AS $$
-- where stepping past either end of a series goes: the last artwork of the series before it, and
-- the first of the series after, in the order the artist dragged the series into. A series with
-- nothing a visitor may be sent to is stepped over, by the same rule get_artwork_neighbours_in_series
-- uses within a series
WITH
    destinations AS (
        SELECT
            series.sort_order AS series_sort_order,
            series.slug AS series_slug,
            junction.sort_order,
            artwork.slug,
            (
                SELECT
                    count(*)::int
                FROM
                    artwork_images AS image
                WHERE
                    image.artwork_id = artwork.id
            ) AS image_count
        FROM
            series
            JOIN artwork_and_series_junction AS junction ON junction.series_id = series.id
            JOIN artworks AS artwork ON artwork.id = junction.artwork_id
        WHERE
            NOT p_only_artworks_with_images
            OR EXISTS (
                SELECT
                FROM
                    artwork_images AS image
                WHERE
                    image.artwork_id = artwork.id
            )
    )
SELECT
    previous_artwork.series_slug,
    previous_artwork.slug,
    previous_artwork.image_count,
    next_artwork.series_slug,
    next_artwork.slug,
    next_artwork.image_count
FROM
    series AS here
    LEFT JOIN LATERAL (
        SELECT
            destination.series_slug,
            destination.slug,
            destination.image_count
        FROM
            destinations AS destination
        WHERE
            destination.series_sort_order < here.sort_order
        ORDER BY
            destination.series_sort_order DESC,
            destination.sort_order DESC
        LIMIT
            1
    ) AS previous_artwork ON true
    LEFT JOIN LATERAL (
        SELECT
            destination.series_slug,
            destination.slug,
            destination.image_count
        FROM
            destinations AS destination
        WHERE
            destination.series_sort_order > here.sort_order
        ORDER BY
            destination.series_sort_order,
            destination.sort_order
        LIMIT
            1
    ) AS next_artwork ON true
WHERE
    here.id = p_series_id;
$$;
