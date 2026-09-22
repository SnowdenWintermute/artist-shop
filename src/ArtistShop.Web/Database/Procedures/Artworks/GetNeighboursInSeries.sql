DROP FUNCTION IF EXISTS get_artwork_neighbours_in_series;

CREATE FUNCTION get_artwork_neighbours_in_series (p_series_id int, p_artwork_id int, p_only_artworks_with_images boolean) RETURNS TABLE (
    previous_name text,
    previous_slug text,
    next_name text,
    next_slug text
) LANGUAGE sql STABLE AS $$
-- the places in the series a visitor may be sent to, filtered once so both sides share the rule.
-- A work with no photograph is not somewhere a visitor can be sent
WITH
    destinations AS (
        SELECT
            junction.sort_order,
            artwork.name,
            artwork.slug
        FROM
            artwork_and_series_junction AS junction
            JOIN artworks AS artwork ON artwork.id = junction.artwork_id
        WHERE
            junction.series_id = p_series_id
            AND (
                NOT p_only_artworks_with_images
                OR EXISTS (
                    SELECT
                    FROM
                        artwork_images AS image
                    WHERE
                        image.artwork_id = artwork.id
                )
            )
    )
    -- The artwork's own place in the series is the anchor, so both sides come off one row and an
    -- artwork that isn't in the series returns nothing at all. The (series_id, sort_order)
    -- constraint means no two artworks share a place, so < and > can't step over one
SELECT
    previous_artwork.name,
    previous_artwork.slug,
    next_artwork.name,
    next_artwork.slug
FROM
    artwork_and_series_junction AS here
    LEFT JOIN LATERAL (
        SELECT
            destination.name,
            destination.slug
        FROM
            destinations AS destination
        WHERE
            destination.sort_order < here.sort_order
        ORDER BY
            destination.sort_order DESC
        LIMIT
            1
    ) AS previous_artwork ON true
    LEFT JOIN LATERAL (
        SELECT
            destination.name,
            destination.slug
        FROM
            destinations AS destination
        WHERE
            destination.sort_order > here.sort_order
        ORDER BY
            destination.sort_order
        LIMIT
            1
    ) AS next_artwork ON true
WHERE
    here.series_id = p_series_id
    AND here.artwork_id = p_artwork_id;
$$;
