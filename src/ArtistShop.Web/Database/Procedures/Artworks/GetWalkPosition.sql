DROP FUNCTION IF EXISTS get_artwork_walk_position;

CREATE FUNCTION get_artwork_walk_position (p_series_id int, p_artwork_id int) RETURNS TABLE (
    earlier_image_count int,
    total_image_count int
) LANGUAGE sql STABLE AS $$
-- where an artwork's images sit among all the images Previous and Next step through, which walk
-- each series in turn the way the artist ordered them, as get_artwork_neighbours does. The
-- lightbox numbers its pictures by it. An artwork in two series is in the walk twice, and one with
-- no images adds nothing, so neither needs a rule of its own. Counting reads every place in the
-- catalogue, but only counts
WITH
    places AS (
        SELECT
            series.sort_order AS series_sort_order,
            junction.sort_order,
            (
                SELECT
                    count(*)::int
                FROM
                    artwork_images AS image
                WHERE
                    image.artwork_id = junction.artwork_id
            ) AS image_count
        FROM
            artwork_and_series_junction AS junction
            JOIN series ON series.id = junction.series_id
    )
SELECT
    coalesce(
        sum(place.image_count) FILTER (
            WHERE
                (place.series_sort_order, place.sort_order) < (here_series.sort_order, here.sort_order)
        ),
        0
    )::int,
    coalesce(sum(place.image_count), 0)::int
FROM
    artwork_and_series_junction AS here
    JOIN series AS here_series ON here_series.id = here.series_id
    CROSS JOIN places AS place
WHERE
    here.series_id = p_series_id
    AND here.artwork_id = p_artwork_id
GROUP BY
    here_series.sort_order,
    here.sort_order;
$$;
