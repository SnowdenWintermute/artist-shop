DROP FUNCTION IF EXISTS get_artwork_neighbours;

CREATE FUNCTION get_artwork_neighbours (p_series_id int, p_artwork_id int, p_only_artworks_with_images boolean) RETURNS TABLE (
    previous_series_slug text,
    previous_artwork_slug text,
    previous_image_count int,
    next_series_slug text,
    next_artwork_slug text,
    next_image_count int
) LANGUAGE sql STABLE AS $$
-- the places in a series a visitor may be sent to, written once so both sides share the rule. A
-- work with no photograph is not somewhere a visitor can be sent. NOT MATERIALIZED puts this into
-- each lookup below as it stands, so each one reads its own series by index instead of the whole
-- catalogue being listed first
WITH
    destinations AS NOT MATERIALIZED (
        SELECT
            junction.series_id,
            junction.sort_order,
            artwork.id AS artwork_id,
            artwork.slug
        FROM
            artwork_and_series_junction AS junction
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
    -- The artwork's own place in the series is the anchor, so both sides come off one row and an
    -- artwork that isn't in the series returns nothing at all. Each side looks within the series
    -- first, and only past its end walks the series the way the artist ordered them, stepping over
    -- any with nothing to see. LIMIT over UNION ALL stops at the first branch that finds a place, so
    -- the walk only runs at an end
SELECT
    previous_series.slug,
    previous_artwork.slug,
    (
        SELECT
            count(*)::int
        FROM
            artwork_images AS image
        WHERE
            image.artwork_id = previous_artwork.artwork_id
    ),
    next_series.slug,
    next_artwork.slug,
    (
        SELECT
            count(*)::int
        FROM
            artwork_images AS image
        WHERE
            image.artwork_id = next_artwork.artwork_id
    )
FROM
    artwork_and_series_junction AS here
    JOIN series AS here_series ON here_series.id = here.series_id
    LEFT JOIN LATERAL (
        (
            SELECT
                destination.*
            FROM
                destinations AS destination
            WHERE
                destination.series_id = here.series_id
                AND destination.sort_order < here.sort_order
            ORDER BY
                destination.sort_order DESC
            LIMIT
                1
        )
        UNION ALL
        (
            SELECT
                last_place.*
            FROM
                series AS earlier_series
                CROSS JOIN LATERAL (
                    SELECT
                        destination.*
                    FROM
                        destinations AS destination
                    WHERE
                        destination.series_id = earlier_series.id
                    ORDER BY
                        destination.sort_order DESC
                    LIMIT
                        1
                ) AS last_place
            WHERE
                earlier_series.sort_order < here_series.sort_order
            ORDER BY
                earlier_series.sort_order DESC
            LIMIT
                1
        )
        LIMIT
            1
    ) AS previous_artwork ON true
    LEFT JOIN series AS previous_series ON previous_series.id = previous_artwork.series_id
    LEFT JOIN LATERAL (
        (
            SELECT
                destination.*
            FROM
                destinations AS destination
            WHERE
                destination.series_id = here.series_id
                AND destination.sort_order > here.sort_order
            ORDER BY
                destination.sort_order
            LIMIT
                1
        )
        UNION ALL
        (
            SELECT
                first_place.*
            FROM
                series AS later_series
                CROSS JOIN LATERAL (
                    SELECT
                        destination.*
                    FROM
                        destinations AS destination
                    WHERE
                        destination.series_id = later_series.id
                    ORDER BY
                        destination.sort_order
                    LIMIT
                        1
                ) AS first_place
            WHERE
                later_series.sort_order > here_series.sort_order
            ORDER BY
                later_series.sort_order
            LIMIT
                1
        )
        LIMIT
            1
    ) AS next_artwork ON true
    LEFT JOIN series AS next_series ON next_series.id = next_artwork.series_id
WHERE
    here.series_id = p_series_id
    AND here.artwork_id = p_artwork_id;
$$;
