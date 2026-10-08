DROP FUNCTION IF EXISTS get_work_walk_position;

CREATE FUNCTION get_work_walk_position (p_collection_id int, p_work_id int) RETURNS TABLE (
    earlier_image_count int,
    total_image_count int
) LANGUAGE sql STABLE AS $$
-- where a work's images sit among all the images Previous and Next step through, which walk
-- each collection in turn the way the artist ordered them, as get_work_neighbours does. The
-- lightbox numbers its pictures by it. A work in two collections is in the walk twice, and one with
-- no images adds nothing, so neither needs a rule of its own. Counting reads every place in the
-- catalogue, but only counts
WITH
    places AS (
        SELECT
            collections.sort_order AS collection_sort_order,
            junction.sort_order,
            (
                SELECT
                    count(*)::int
                FROM
                    work_images AS image
                WHERE
                    image.work_id = junction.work_id
            ) AS image_count
        FROM
            work_and_collection_junction AS junction
            JOIN collections ON collections.id = junction.collection_id
    )
SELECT
    coalesce(
        sum(place.image_count) FILTER (
            WHERE
                (place.collection_sort_order, place.sort_order) < (here_collection.sort_order, here.sort_order)
        ),
        0
    )::int,
    coalesce(sum(place.image_count), 0)::int
FROM
    work_and_collection_junction AS here
    JOIN collections AS here_collection ON here_collection.id = here.collection_id
    CROSS JOIN places AS place
WHERE
    here.collection_id = p_collection_id
    AND here.work_id = p_work_id
GROUP BY
    here_collection.sort_order,
    here.sort_order;
$$;
